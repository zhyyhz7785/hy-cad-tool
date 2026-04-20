using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// <c>.roaddesign.json</c> 的低层读写服务。
    ///
    /// 仅负责 I/O + 序列化反序列化，不涉及事件发布 / 文件监听。
    /// v1.1 起各命令在 <c>tr.Commit()</c> 后通过 <see cref="SaveForDocument"/> 同步落盘（取消原防抖持久化服务）。
    ///
    /// 决策 1（单文件 JSON）：每个项目一个 <c>.roaddesign.json</c>，与 DWG 同级目录。
    /// 决策 3（事件总线）：本服务只读写文件，事件发布方是 <c>*Service</c> 和 <c>*Registry</c>。
    /// </summary>
    public sealed class RoadJsonExportService
    {
        private readonly JsonSerializerSettings _settings;

        public RoadJsonExportService()
        {
            _settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                Culture = System.Globalization.CultureInfo.InvariantCulture
                // ObjectCreationHandling 保持默认 Auto：
                // - 对于 getter-only 的 List<T> 属性（RoadDesign.Alignments / Templates / ...），
                //   Auto 会 reuse 构造时创建的空 List 并 Add 反序列化结果，这是唯一可行的路径。
                // - 对于"值对象内部有只读集合"的类型（Polyline3D.Vertices / Mesh3D.Triangles），
                //   必须在具体属性上显式标 [JsonProperty(ObjectCreationHandling.Replace)]，
                //   强制 Newtonsoft 走 [JsonConstructor]，见 Alignment.Centerline。
            };
        }

        /// <summary>
        /// 推断 <c>.roaddesign.json</c> 默认路径（同 DWG 同名）。
        /// </summary>
        public static string GetDefaultJsonPath(string dwgPath)
        {
            if (string.IsNullOrEmpty(dwgPath)) return null;
            var dir = Path.GetDirectoryName(dwgPath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(dwgPath);
            return Path.Combine(dir, name + ".roaddesign.json");
        }

        /// <summary>
        /// 命令收尾的"按文档名同步写盘"便利方法（v1.1 替代原防抖持久化服务）。
        ///
        /// 规则（无副作用、静默跳过）：
        /// - <paramref name="roadDesign"/> 为空 或 <see cref="RoadDesign.IsEmpty"/> ⇒ 返回 null，不写；
        /// - <paramref name="documentName"/> 解析不出路径（DWG 尚未保存）⇒ 返回 null，不写；
        /// - 路径可解析且 design 有内容 ⇒ 调 <see cref="Save(RoadDesign,string)"/> 落盘，返回路径。
        ///
        /// 意图：封装"空跳过 + 路径失败不抛"两条规则，让命令层收尾只写一行。
        /// </summary>
        public string SaveForDocument(RoadDesign roadDesign, string documentName)
        {
            if (roadDesign == null || roadDesign.IsEmpty) return null;
            if (string.IsNullOrWhiteSpace(documentName)) return null;
            var path = GetDefaultJsonPath(documentName);
            if (string.IsNullOrWhiteSpace(path)) return null;
            Save(roadDesign, path);
            return path;
        }

        /// <summary>
        /// 「保存最后的文（件）」入口（v1.2 决策 4）：
        /// 落盘 JSON 之前，先把 <see cref="Domain.Models.Road.AlignmentSourceKind.PiTable"/> /
        /// <see cref="Domain.Models.Road.AlignmentSourceKind.Unknown"/> 类的"已无对应 HY_ROAD Polyline"的孤儿
        /// Alignment 从 <paramref name="design"/> 中删除，再写盘。
        ///
        /// <see cref="Domain.Models.Road.AlignmentSourceKind.UserPicked"/> 的草稿线位永远保留——它本就允许
        /// "图上无正式 Polyline、仅 Domain + 预览实体"的状态。
        /// </summary>
        public string SaveForDocumentSyncDwg(Document doc, RoadDesign design)
        {
            if (doc == null) return null;
            if (design == null || design.IsEmpty) return null;
            try
            {
                int purged = PurgeOrphans(doc, design);
                if (purged > 0)
                    design.LastModifiedUtc = DateTime.UtcNow;
            }
            catch
            {
                // PurgeOrphans 任何失败（事务、Xdata 异常）都不阻断保存。
            }
            return SaveForDocument(design, doc.Name);
        }

        /// <summary>
        /// 扫描 ModelSpace 收集所有挂 HY_ROAD KIND=Alignment 的 Polyline ID 集合，
        /// 然后剔除 <paramref name="design"/> 里 <c>Source.Kind != UserPicked</c> 且不在该集合中的 Alignment。
        /// 返回剔除条数。
        /// </summary>
        public int PurgeOrphans(Document doc, RoadDesign design)
        {
            if (doc == null || design == null || design.Alignments.Count == 0) return 0;

            var live = new HashSet<Guid>();
            var db = doc.Database;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    DBObject ent;
                    try { ent = tr.GetObject(id, OpenMode.ForRead); }
                    catch { continue; }
                    if (!(ent is Polyline)) continue;
                    var kind = HyRoadXdata.ReadKind(tr, ent);
                    if (!string.Equals(kind, HyRoadXdata.KindAlignment, StringComparison.Ordinal)) continue;
                    var gid = HyRoadXdata.ReadId(tr, ent);
                    if (gid != Guid.Empty) live.Add(gid);
                }
                tr.Commit();
            }

            int removed = 0;
            for (int i = design.Alignments.Count - 1; i >= 0; i--)
            {
                var aln = design.Alignments[i];
                if (aln == null) continue;

                var kind = aln.Source?.Kind ?? AlignmentSourceKind.Unknown;
                if (kind == AlignmentSourceKind.UserPicked) continue;       // 草稿不算孤儿
                if (live.Contains(aln.Id)) continue;                        // 实际仍在 DWG

                design.Alignments.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// 将 RoadDesign 序列化到 JSON 文件。
        /// 采用"临时文件 + 原子替换"避免写入过程中崩溃导致的空文件。
        /// </summary>
        public void Save(RoadDesign roadDesign, string path)
        {
            if (roadDesign == null) throw new ArgumentNullException(nameof(roadDesign));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path 不能为空", nameof(path));

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            string content = JsonConvert.SerializeObject(roadDesign, _settings);

            File.WriteAllText(tmp, content, new System.Text.UTF8Encoding(false));

            // 原子替换
            if (File.Exists(path))
            {
                File.Replace(tmp, path, path + ".bak", ignoreMetadataErrors: true);
                try { File.Delete(path + ".bak"); } catch { /* 忽略备份清理失败 */ }
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        /// <summary>
        /// 从 JSON 文件反序列化 RoadDesign。文件不存在返回 null。
        /// </summary>
        public RoadDesign Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!File.Exists(path)) return null;

            string content = File.ReadAllText(path);
            var design = JsonConvert.DeserializeObject<RoadDesign>(content, _settings);

            if (design != null
                && !string.IsNullOrEmpty(design.Schema)
                && string.Compare(design.Schema, SchemaVersion.MinimumSupported, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException(
                    $"不兼容的 schema 版本：{design.Schema}（最低支持 {SchemaVersion.MinimumSupported}）。");
            }
            return design;
        }
    }
}

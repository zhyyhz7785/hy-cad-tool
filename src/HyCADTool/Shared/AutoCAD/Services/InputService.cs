using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Windows;
using HyCADTool.Shell.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Services
{
    /// <summary>
    /// AutoCAD 用户输入服务实现
    /// </summary>
    public class InputService : IInputService
    {
        // === 点输入 ===

        public (double X, double Y, double Z)? GetUserPoint(string promptMessage = "请输入一个点: ")
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var ed = doc.Editor;
            var ppr = ed.GetPoint($"\n{promptMessage}");
            
            if (ppr.Status == PromptStatus.OK)
            {
                var point = ppr.Value;
                return (point.X, point.Y, point.Z);
            }

            return null;
        }

        public (double X, double Y, double Z)? GetUserPointFromBase((double X, double Y, double Z) basePoint, string promptMessage = "请输入相对点: ")
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var ed = doc.Editor;
            var ppo = new PromptPointOptions($"\n{promptMessage}")
            {
                BasePoint = new Point3d(basePoint.X, basePoint.Y, basePoint.Z),
                UseBasePoint = true
            };

            var ppr = ed.GetPoint(ppo);
            
            if (ppr.Status == PromptStatus.OK)
            {
                var point = ppr.Value;
                return (point.X, point.Y, point.Z);
            }

            return null;
        }

        public List<(double X, double Y, double Z)> GetUserPoints(string promptMessage = "请输入点: ", bool allowClose = false)
        {
            var points = new List<(double X, double Y, double Z)>();
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return points;

            var ed = doc.Editor;
            
            while (true)
            {
                var ppo = new PromptPointOptions($"\n{promptMessage} (回车结束): ");
                
                // 如果已有点，设置基点
                if (points.Count > 0)
                {
                    var lastPoint = points.Last();
                    ppo.BasePoint = new Point3d(lastPoint.X, lastPoint.Y, lastPoint.Z);
                    ppo.UseBasePoint = true;
                }

                // 如果允许闭合且有足够的点，添加闭合选项
                if (allowClose && points.Count >= 2)
                {
                    ppo.Keywords.Add("闭合");
                    ppo.Keywords.Add("C");
                    ppo.Keywords.Default = "闭合";
                }

                var ppr = ed.GetPoint(ppo);
                
                if (ppr.Status == PromptStatus.OK)
                {
                    var point = ppr.Value;
                    points.Add((point.X, point.Y, point.Z));
                }
                else if (ppr.Status == PromptStatus.Keyword)
                {
                    if (ppr.StringResult.ToUpper() == "C" || ppr.StringResult == "闭合")
                    {
                        // 闭合多段线
                        break;
                    }
                }
                else
                {
                    // 用户取消或回车结束
                    break;
                }
            }

            return points;
        }

        // === 数值输入 ===

        public double? GetUserDistance(string promptMessage = "请输入距离: ", (double X, double Y, double Z)? basePoint = null)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var ed = doc.Editor;
            var pdo = new PromptDistanceOptions($"\n{promptMessage}");
            
            if (basePoint.HasValue)
            {
                pdo.BasePoint = new Point3d(basePoint.Value.X, basePoint.Value.Y, basePoint.Value.Z);
                pdo.UseBasePoint = true;
            }

            var pdr = ed.GetDistance(pdo);
            
            return pdr.Status == PromptStatus.OK ? (double?)pdr.Value : null;
        }

        public double? GetUserAngle(string promptMessage = "请输入角度: ", (double X, double Y, double Z)? basePoint = null)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var ed = doc.Editor;
            var pao = new PromptAngleOptions($"\n{promptMessage}");
            
            if (basePoint.HasValue)
            {
                pao.BasePoint = new Point3d(basePoint.Value.X, basePoint.Value.Y, basePoint.Value.Z);
                pao.UseBasePoint = true;
            }

            var par = ed.GetAngle(pao);
            
            return par.Status == PromptStatus.OK ? (double?)par.Value : null;
        }

        public int? GetUserInteger(string promptMessage = "请输入整数: ", int? defaultValue = null)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var ed = doc.Editor;
            var pio = new PromptIntegerOptions($"\n{promptMessage}");
            
            if (defaultValue.HasValue)
            {
                pio.DefaultValue = defaultValue.Value;
                pio.UseDefaultValue = true;
            }

            var pir = ed.GetInteger(pio);
            
            return pir.Status == PromptStatus.OK ? (int?)pir.Value : null;
        }

        public double? GetUserDouble(string promptMessage = "请输入数值: ", double? defaultValue = null)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var ed = doc.Editor;
            var pdo = new PromptDoubleOptions($"\n{promptMessage}");
            
            if (defaultValue.HasValue)
            {
                pdo.DefaultValue = defaultValue.Value;
                pdo.UseDefaultValue = true;
            }

            var pdr = ed.GetDouble(pdo);
            
            return pdr.Status == PromptStatus.OK ? (double?)pdr.Value : null;
        }

        // === 文字输入 ===

        public string GetUserString(string promptMessage = "请输入字符串: ", string defaultValue = null, bool allowSpaces = true)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var ed = doc.Editor;
            var pso = new PromptStringOptions($"\n{promptMessage}")
            {
                AllowSpaces = allowSpaces
            };
            
            if (!string.IsNullOrEmpty(defaultValue))
            {
                pso.DefaultValue = defaultValue;
                pso.UseDefaultValue = true;
            }

            var psr = ed.GetString(pso);
            
            return psr.Status == PromptStatus.OK ? psr.StringResult : null;
        }

        public string GetUserKeyword(string promptMessage, string[] keywords, string defaultKeyword = null)
        {
            if (keywords == null || keywords.Length == 0)
                throw new ArgumentException("Keywords cannot be null or empty", nameof(keywords));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var ed = doc.Editor;
            var pko = new PromptKeywordOptions($"\n{promptMessage}");
            
            foreach (var keyword in keywords)
            {
                pko.Keywords.Add(keyword);
            }

            if (!string.IsNullOrEmpty(defaultKeyword) && keywords.Contains(defaultKeyword))
            {
                pko.Keywords.Default = defaultKeyword;
            }

            var pkr = ed.GetKeywords(pko);
            
            return pkr.Status == PromptStatus.OK ? pkr.StringResult : null;
        }

        // === 实体选择 ===

        public string GetUserEntity(string promptMessage = "请选择实体: ", string entityType = null)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var ed = doc.Editor;
            var peo = new PromptEntityOptions($"\n{promptMessage}");
            
            if (!string.IsNullOrEmpty(entityType))
            {
                peo.SetRejectMessage($"\n请选择{entityType}类型的实体。");
                // 这里可以添加类型过滤逻辑
            }

            var per = ed.GetEntity(peo);
            
            return per.Status == PromptStatus.OK ? per.ObjectId.ToString() : null;
        }

        public List<string> GetUserEntities(string promptMessage = "请选择实体: ", string[] entityTypes = null)
        {
            var entities = new List<string>();
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return entities;

            var ed = doc.Editor;
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = $"\n{promptMessage}"
            };

            SelectionFilter filter = null;
            if (entityTypes != null && entityTypes.Length > 0)
            {
                var filterValues = entityTypes.Select(type => new TypedValue((int)DxfCode.Start, type)).ToArray();
                filter = new SelectionFilter(filterValues);
            }

            var psr = filter != null ? ed.GetSelection(pso, filter) : ed.GetSelection(pso);
            
            if (psr.Status == PromptStatus.OK)
            {
                entities.AddRange(psr.Value.GetObjectIds().Select(id => id.ToString()));
            }

            return entities;
        }

        // === 特殊输入 ===

        public bool GetUserConfirmation(string promptMessage = "是否确认? ", bool defaultValue = true)
        {
            var keywords = new[] { "是", "否", "Y", "N" };
            var defaultKeyword = defaultValue ? "是" : "否";
            
            var result = GetUserKeyword($"{promptMessage} [是(Y)/否(N)]:", keywords, defaultKeyword);
            
            if (string.IsNullOrEmpty(result))
                return defaultValue;
                
            return result.ToUpper() == "Y" || result == "是";
        }

        public string GetUserFilePath(string promptMessage = "请选择文件: ", string fileExtension = "*.*", bool isOpen = true)
        {
            try
            {
                var dialog = isOpen 
                    ? (System.Windows.Forms.FileDialog)new System.Windows.Forms.OpenFileDialog()
                    : new System.Windows.Forms.SaveFileDialog();
                    
                dialog.Title = promptMessage;
                dialog.Filter = $"文件 ({fileExtension})|{fileExtension}|所有文件 (*.*)|*.*";
                dialog.FilterIndex = 1;
                
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return dialog.FileName;
                }
                
                return null;
            }
            catch (System.Exception ex)
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage($"\n文件对话框错误: {ex.Message}");
                return null;
            }
        }

        public ((double X, double Y, double Z) Point1, (double X, double Y, double Z) Point2)? GetUserWindow(string promptMessage = "请选择窗口: ")
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var ed = doc.Editor;
            
            // 获取第一个角点
            var ppo1 = new PromptPointOptions($"\n{promptMessage} - 选择第一个角点: ");
            var ppr1 = ed.GetPoint(ppo1);
            
            if (ppr1.Status != PromptStatus.OK)
                return null;

            // 获取第二个角点
            var ppo2 = new PromptCornerOptions("\n选择对角点: ", ppr1.Value);
            var ppr2 = ed.GetCorner(ppo2);
            
            if (ppr2.Status != PromptStatus.OK)
                return null;

            var point1 = ppr1.Value;
            var point2 = ppr2.Value;
            
            return ((point1.X, point1.Y, point1.Z), (point2.X, point2.Y, point2.Z));
        }
    }
}

//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.Tools;
//using Newtonsoft.Json;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using Exception = Autodesk.AutoCAD.Runtime.Exception;
//namespace HyCADTool.Command
//{
//    public static partial class HyCommand
//    {
//        // 判断点是否在多段线内部（简单实现）
//        private static bool IsPointInside(this Polyline pline, Point3d point)
//        {
//            int intersections = 0;
//            int nvert = pline.NumberOfVertices;
//            for (int i = 0, j = nvert - 1; i < nvert; j = i++)
//            {
//                Point3d pi = pline.GetPoint3dAt(i);
//                Point3d pj = pline.GetPoint3dAt(j);
//                if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
//                    (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
//                {
//                    intersections++;
//                }
//            }
//            return (intersections % 2) == 1; // 奇数次相交表示点在内部
//        }
//        [CommandMethod("hyab_c1")]
//        public static void ConstructBaseDataFilter()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            // 定义图层过滤器
//            TypedValue[] filterList = new TypedValue[]
//            {
//        new TypedValue((int)DxfCode.Operator, "<OR"),
//        new TypedValue((int)DxfCode.Start, "LWPOLYLINE"), // 多段线
//        new TypedValue((int)DxfCode.Start, "CIRCLE"),      // 圆
//        new TypedValue((int)DxfCode.Operator, "OR>"),
//        new TypedValue((int)DxfCode.Operator, "<OR"),
//        new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓_轮廓"), // 多段线图层
//        new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓*"),     // 螺栓图层（支持型号后缀）
//        new TypedValue((int)DxfCode.Operator, "OR>")
//            };
//            SelectionFilter filter = new SelectionFilter(filterList);
//            PromptSelectionResult selRes = ed.GetSelection(filter);
//            if (selRes.Status != PromptStatus.OK) return;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                // 第一步：分别收集 Polyline 和 Circle，并排序
//                List<Polyline> polylineList = new List<Polyline>();
//                List<Circle> circleList = new List<Circle>();
//                foreach (SelectedObject selObj in selRes.Value)
//                {
//                    Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
//                    if (ent is Polyline pline && ent.Layer == "00_Hy_螺栓_轮廓")
//                    {
//                        polylineList.Add(pline);
//                    }
//                    else if (ent is Circle circle && ent.Layer.StartsWith("00_Hy_螺栓"))
//                    {
//                        circleList.Add(circle);
//                    }
//                }
//                // 按 X 和 Y 坐标升序排序
//                polylineList.Sort((p1, p2) =>
//                {
//                    Point3d p1Start = p1.StartPoint;
//                    Point3d p2Start = p2.StartPoint;
//                    int xCompare = p1Start.X.CompareTo(p2Start.X);
//                    return xCompare != 0 ? xCompare : p1Start.Y.CompareTo(p2Start.Y);
//                });
//                circleList.Sort((c1, c2) =>
//                {
//                    Point3d c1Center = c1.Center;
//                    Point3d c2Center = c2.Center;
//                    int xCompare = c1Center.X.CompareTo(c2Center.X);
//                    return xCompare != 0 ? xCompare : c1Center.Y.CompareTo(c2Center.Y);
//                });
//                // 第二步：遍历 Polyline 并关联 Circle
//                foreach (Polyline pline in polylineList)
//                {
//                    List<BoltData> bolts = new List<BoltData>();
//                    foreach (Circle circle in circleList)
//                    {
//                        if (IsPointInside(pline, circle.Center))
//                        {
//                            string model = "1"; // 默认型号
//                            bool hasModel = false;
//                            // 从 Circle 的 ExtensionDictionary 中读取型号
//                            AnchorBolt anchorBolt = ReadAnchorBoltFromExtensionDictionary(tr, circle, ed);
//                            if (anchorBolt != null)
//                            {
//                                model = anchorBolt.Model;
//                                hasModel = true;
//                            }
//                            if (!hasModel)
//                            {
//                                circle.UpgradeOpen();
//                                circle.ColorIndex = 1; // 红色
//                                ed.WriteMessage($"\n警告: 圆 {circle.ObjectId} 没有型号数据，已变为红色，请赋值！");
//                                continue;
//                            }
//                            BoltData bolt = new BoltData
//                            {
//                                Id = circle.ObjectId,
//                                Model = model
//                            };
//                            bolts.Add(bolt);
//                        }
//                    }
//                    // 将 BoltData 写入 Polyline 的 ExtensionDictionary
//                    if (bolts.Count > 0)
//                    {
//                        WriteBoltsToExtensionDictionary(tr, pline, bolts, ed);
//                    }
//                }
//                tr.Commit();
//            }
//        }
//        // 抽象方法：从 ExtensionDictionary 读取 AnchorBolt 数据
//        private static AnchorBolt ReadAnchorBoltFromExtensionDictionary(Transaction tr, Circle circle, Editor ed)
//        {
//            if (circle.ExtensionDictionary.IsValid &&
//                tr.GetObject(circle.ExtensionDictionary, OpenMode.ForRead) is DBDictionary extDict &&
//                extDict.Contains("AnchorBolt"))
//            {
//                Xrecord xrec = tr.GetObject(extDict.GetAt("AnchorBolt"), OpenMode.ForRead) as Xrecord;
//                if (xrec != null && xrec.Data != null && xrec.Data.AsArray().Length > 0)
//                {
//                    TypedValue tv = xrec.Data.AsArray()[0];
//                    if (tv.TypeCode == (int)DxfCode.Text)
//                    {
//                        string jsonData = tv.Value.ToString();
//                        return JsonConvert.DeserializeObject<AnchorBolt>(jsonData);
//                    }
//                }
//            }
//            return null; // 未找到数据时返回 null
//        }
//        // 抽象方法：将 BoltData 列表写入 ExtensionDictionary
//        private static void WriteBoltsToExtensionDictionary(Transaction tr, Polyline pline, List<BoltData> bolts, Editor ed)
//        {
//            pline.UpgradeOpen(); // 提升权限以修改对象
//            DBDictionary extDict;
//            if (!pline.ExtensionDictionary.IsValid)
//            {
//                pline.CreateExtensionDictionary();
//                extDict = tr.GetObject(pline.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
//            }
//            else
//            {
//                extDict = tr.GetObject(pline.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
//            }
//            string jsonData = JsonConvert.SerializeObject(bolts);
//            Xrecord xrec = new Xrecord();
//            xrec.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, jsonData));
//            if (extDict.Contains("BoltData"))
//            {
//                extDict.Remove("BoltData");
//            }
//            extDict.SetAt("BoltData", xrec);
//            tr.AddNewlyCreatedDBObject(xrec, true);
//        }
//        [CommandMethod("HYAB_c2")]
//        public static void HighlightBoltData()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            var pline = db.SelectAEntity<Polyline>();
//            if (pline == null)
//            {
//                ed.WriteMessage("\n未选择多段线，操作取消！");
//                return;
//            }
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                // 检查是否有 ExtensionDictionary
//                if (!pline.ExtensionDictionary.IsValid)
//                {
//                    ed.WriteMessage($"\n多段线 {pline.ObjectId} 没有 ExtensionDictionary。");
//                    tr.Commit();
//                    return;
//                }
//                // 读取 BoltData 数据
//                List<BoltData> bolts = ReadBoltsFromExtensionDictionary(tr, pline, ed);
//                if (bolts == null || bolts.Count == 0)
//                {
//                    ed.WriteMessage($"\n多段线 {pline.ObjectId} 的 ExtensionDictionary 中没有 BoltData 数据。");
//                    tr.Commit();
//                    return;
//                }
//                // 高亮显示 BoltData 中的 ObjectId 对应的对象
//                ed.WriteMessage($"\n多段线 {pline.ObjectId} 包含以下 BoltData：");
//                foreach (BoltData bolt in bolts)
//                {
//                    if (bolt.Id.IsValid && !bolt.Id.IsErased)
//                    {
//                        Entity entity = tr.GetObject(bolt.Id, OpenMode.ForRead) as Entity;
//                        if (entity != null)
//                        {
//                            entity.UpgradeOpen();
//                            entity.Highlight(); // 高亮显示
//                            ed.WriteMessage($"\n - BoltData ObjectId: {bolt.Id}, Model: {bolt.Model}");
//                        }
//                        else
//                        {
//                            ed.WriteMessage($"\n - BoltData ObjectId: {bolt.Id} 已无效或被删除。");
//                        }
//                    }
//                }
//                tr.Commit();
//            }
//        }
//        // 从 ExtensionDictionary 读取 BoltData 列表
//        private static List<BoltData> ReadBoltsFromExtensionDictionary(Transaction tr, Polyline pline, Editor ed)
//        {
//            if (pline.ExtensionDictionary.IsValid &&
//                tr.GetObject(pline.ExtensionDictionary, OpenMode.ForRead) is DBDictionary extDict &&
//                extDict.Contains("BoltData"))
//            {
//                Xrecord xrec = tr.GetObject(extDict.GetAt("BoltData"), OpenMode.ForRead) as Xrecord;
//                if (xrec != null && xrec.Data != null && xrec.Data.AsArray().Length > 0)
//                {
//                    TypedValue tv = xrec.Data.AsArray()[0];
//                    if (tv.TypeCode == (int)DxfCode.Text)
//                    {
//                        string jsonData = tv.Value.ToString();
//                        try
//                        {
//                            return JsonConvert.DeserializeObject<List<BoltData>>(jsonData);
//                        }
//                        catch (Exception ex)
//                        {
//                            ed.WriteMessage($"\n解析 BoltData 数据失败: {ex.Message}");
//                            return null;
//                        }
//                    }
//                }
//            }
//            return null; // 未找到数据时返回 null
//        }
//        [CommandMethod("HYAB_ConstructAxis")]
//        public static void ConstructAxisCommand()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Editor ed = doc.Editor;
//            List<AxisData> axes = ConstructAxis();
//            if (axes != null && axes.Count > 0)
//            {
//                ed.WriteMessage($"\n成功构造 {axes.Count} 条轴线！");
//            }
//            else
//            {
//                ed.WriteMessage("\n轴线构造失败或未选择任何直线！");
//            }
//        }
//        private static List<AxisData> ConstructAxis()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            PromptSelectionOptions pso = new PromptSelectionOptions();
//            pso.MessageForAdding = "\n请选择一条或多条直线作为轴线: ";
//            pso.AllowDuplicates = false;
//            TypedValue[] filterList = new TypedValue[]
//            {
//                new TypedValue((int)DxfCode.Start, "LINE")
//            };
//            SelectionFilter filter = new SelectionFilter(filterList);
//            PromptSelectionResult selRes = ed.GetSelection(pso, filter);
//            if (selRes.Status != PromptStatus.OK) return null;
//            List<AxisData> axes = new List<AxisData>();
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                foreach (SelectedObject selObj in selRes.Value)
//                {
//                    Line line = tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) as Line;
//                    if (line == null) continue;
//                    AxisData axisData = new AxisData();
//                    string jsonData = JsonConvert.SerializeObject(axisData);
//                    ed.WriteMessage($"\n调试: 写入 AxisData = {jsonData}");
//                    if (!line.ExtensionDictionary.IsValid)
//                    {
//                        line.CreateExtensionDictionary();
//                    }
//                    DBDictionary extDict = tr.GetObject(line.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
//                    using (Xrecord xrec = new Xrecord())
//                    {
//                        xrec.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, jsonData));
//                        extDict.SetAt("AxisData", xrec);
//                        tr.AddNewlyCreatedDBObject(xrec, true);
//                    }
//                    axes.Add(axisData);
//                }
//                tr.Commit();
//            }
//            // 在 ConstructAxis 中
//            return axes;
//        }
//        [CommandMethod("HYAB_InitializeAxis")]
//        public static void InitializeAxisCommand()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Editor ed = doc.Editor;
//            AxisData axis = InitializeAxis();
//            if (axis != null)
//            {
//                ed.WriteMessage($"\n轴线初始化完成，底座数量: {axis.Bases.Count}");
//            }
//            else
//            {
//                ed.WriteMessage("\n轴线初始化失败或直线没有附加数据！");
//            }
//        }
//        private static AxisData InitializeAxis()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            PromptEntityOptions peo = new PromptEntityOptions("\n请选择一条轴线（直线）: ");
//            peo.SetRejectMessage("\n必须选择直线！");
//            peo.AddAllowedClass(typeof(Line), true);
//            PromptEntityResult per = ed.GetEntity(peo);
//            if (per.Status != PromptStatus.OK) return null;
//            AxisData axis = null;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                Line line = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as Line;
//                if (line == null) return null;
//                if (line.ExtensionDictionary.IsValid)
//                {
//                    DBDictionary extDict = tr.GetObject(line.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
//                    if (extDict.Contains("AxisData"))
//                    {
//                        Xrecord xrec = tr.GetObject(extDict.GetAt("AxisData"), OpenMode.ForRead) as Xrecord;
//                        if (xrec != null && xrec.Data != null && xrec.Data.AsArray().Length > 0)
//                        {
//                            TypedValue tv = xrec.Data.AsArray()[0];
//                            if (tv.TypeCode == (int)DxfCode.Text)
//                            {
//                                string jsonData = tv.Value.ToString();
//                                ed.WriteMessage($"\n调试: 读取的 AxisData = {jsonData}");
//                                axis = JsonConvert.DeserializeObject<AxisData>(jsonData);
//                            }
//                        }
//                    }
//                }
//                if (axis == null)
//                {
//                    ed.WriteMessage($"\n错误: 直线 {per.ObjectId} 没有附加的 AxisData 数据！");
//                    tr.Commit();
//                    return null;
//                }
//                PromptSelectionOptions pso = new PromptSelectionOptions();
//                pso.MessageForAdding = "\n请选择多个底座 (Polyline): ";
//                TypedValue[] filterList = new TypedValue[]
//{
//    new TypedValue((int)DxfCode.Operator, "<AND"),              // 开始 AND 组
//    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),          // 实体类型为多段线
//    new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓_轮廓"), // 图层名称
//    new TypedValue((int)DxfCode.Operator, "AND>")              // 结束 AND 组
//};
//                SelectionFilter filter = new SelectionFilter(filterList);
//                PromptSelectionResult selRes = ed.GetSelection(pso, filter);
//                if (selRes.Status != PromptStatus.OK)
//                {
//                    tr.Commit();
//                    return axis;
//                }
//                foreach (SelectedObject selObj in selRes.Value)
//                {
//                    Polyline pline = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
//                    if (pline != null)
//                    {
//                        BaseData baseData = new BaseData
//                        {
//                            Id = selObj.ObjectId,
//                            Bolts = new List<BoltData>()
//                        };
//                        // 从 Polyline 的 ExtensionDictionary 读取 BoltData
//                        List<BoltData> bolts = ReadBoltsFromExtensionDictionary(tr, pline, ed);
//                        if (bolts != null && bolts.Count > 0)
//                        {
//                            baseData.Bolts.AddRange(bolts);
//                            axis.Bolts.AddRange(bolts); // 汇总到 AxisData.Bolts
//                        }
//                        ed.WriteMessage($"\n调试: 添加 BaseData, Id = {baseData.Id.Handle.ToString()}, Bolts.Count = {baseData.Bolts.Count}");
//                        axis.Bases.Add(baseData);
//                    }
//                }
//                string updatedJsonData = JsonConvert.SerializeObject(axis);
//                ed.WriteMessage($"\n调试: 更新后的 AxisData = {updatedJsonData}");
//                DBDictionary extDictWrite = tr.GetObject(line.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
//                if (extDictWrite.Contains("AxisData"))
//                {
//                    Xrecord xrec = tr.GetObject(extDictWrite.GetAt("AxisData"), OpenMode.ForWrite) as Xrecord;
//                    xrec.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, updatedJsonData));
//                }
//                tr.Commit();
//            }
//            return axis;
//        }
//        [CommandMethod("HYAB_DeleteBaseDataFromAxis")]
//        public static void DeleteBaseDataFromAxisCommand()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Editor ed = doc.Editor;
//            AxisData axis = InitializeAxis();
//            if (axis == null)
//            {
//                ed.WriteMessage("\n轴线初始化失败，无法删除底座！");
//                return;
//            }
//            DeleteBaseDataFromAxis(axis);
//            ed.WriteMessage($"\n底座删除完成，剩余底座数量: {axis.Bases.Count}");
//        }
//        private static void DeleteBaseDataFromAxis(AxisData axis)
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            PromptSelectionOptions pso = new PromptSelectionOptions();
//            pso.MessageForAdding = "\n请选择要删除的底座 (Polyline): ";
//            TypedValue[] filterList = new TypedValue[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") };
//            SelectionFilter filter = new SelectionFilter(filterList);
//            PromptSelectionResult selRes = ed.GetSelection(pso, filter);
//            if (selRes.Status != PromptStatus.OK) return;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                foreach (SelectedObject selObj in selRes.Value)
//                {
//                    BaseData baseToRemove = axis.Bases.FirstOrDefault(b => b.Id == selObj.ObjectId);
//                    if (baseToRemove != null)
//                    {
//                        axis.Bases.Remove(baseToRemove);
//                    }
//                }
//                // 更新直线的 ExtensionDictionary
//                Line line = tr.GetObject(axis.Bases.FirstOrDefault()?.Id ?? ObjectId.Null, OpenMode.ForWrite) as Line;
//                if (line != null && line.ExtensionDictionary.IsValid)
//                {
//                    string updatedJsonData = JsonConvert.SerializeObject(axis);
//                    DBDictionary extDict = tr.GetObject(line.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
//                    if (extDict.Contains("AxisData"))
//                    {
//                        Xrecord xrec = tr.GetObject(extDict.GetAt("AxisData"), OpenMode.ForWrite) as Xrecord;
//                        xrec.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, updatedJsonData));
//                    }
//                }
//                tr.Commit();
//            }
//        }
//        [CommandMethod("HYAB_DisplayStructure")]
//        public static void DisplayStructureCommand()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Editor ed = doc.Editor;
//            AxisData axis = GetAxisFromLine();
//            if (axis == null)
//            {
//                ed.WriteMessage("\n无法读取轴线数据或直线没有附加数据，无法显示结构！");
//                return;
//            }
//            DisplayStructure(axis);
//            ed.WriteMessage("\n结构高亮显示完成！");
//        }
//        private static AxisData GetAxisFromLine()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            PromptEntityOptions peo = new PromptEntityOptions("\n请选择一条轴线（直线）: ");
//            peo.SetRejectMessage("\n必须选择直线！");
//            peo.AddAllowedClass(typeof(Line), true);
//            PromptEntityResult per = ed.GetEntity(peo);
//            if (per.Status != PromptStatus.OK) return null;
//            AxisData axis = null;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                Line line = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Line;
//                if (line == null) return null;
//                if (line.ExtensionDictionary.IsValid)
//                {
//                    DBDictionary extDict = tr.GetObject(line.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
//                    if (extDict.Contains("AxisData"))
//                    {
//                        Xrecord xrec = tr.GetObject(extDict.GetAt("AxisData"), OpenMode.ForRead) as Xrecord;
//                        if (xrec != null && xrec.Data != null && xrec.Data.AsArray().Length > 0)
//                        {
//                            TypedValue tv = xrec.Data.AsArray()[0];
//                            if (tv.TypeCode == (int)DxfCode.Text)
//                            {
//                                string jsonData = tv.Value.ToString();
//                                // 添加调试输出
//                                ed.WriteMessage($"\n调试: JSON 数据 = {jsonData}");
//                                try
//                                {
//                                    axis = JsonConvert.DeserializeObject<AxisData>(jsonData);
//                                }
//                                catch (Exception ex)
//                                {
//                                    ed.WriteMessage($"\n反序列化失败: {ex.Message}");
//                                    throw; // 保留异常以便进一步调试
//                                }
//                            }
//                        }
//                    }
//                }
//                tr.Commit();
//            }
//            return axis;
//        }
//        private static void DisplayStructure(AxisData axis)
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                foreach (BaseData baseData in axis.Bases)
//                {
//                    Polyline pline = tr.GetObject(baseData.Id, OpenMode.ForRead) as Polyline;
//                    if (pline != null)
//                    {
//                        pline.UpgradeOpen();
//                        pline.Highlight(); // 高亮底座（多段线）
//                    }
//                    foreach (BoltData bolt in baseData.Bolts)
//                    {
//                        Circle circle = tr.GetObject(bolt.Id, OpenMode.ForRead) as Circle;
//                        if (circle != null)
//                        {
//                            circle.UpgradeOpen();
//                            circle.Highlight(); // 高亮螺栓（圆）
//                        }
//                    }
//                }
//                tr.Commit();
//            }
//        }
//    }
//}
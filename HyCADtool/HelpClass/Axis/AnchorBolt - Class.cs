//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using Newtonsoft.Json;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//namespace HyCADTool.Command
//{
//    public class AnchorBolt
//    {
//        public string Model { get; set; } // 型号 (01-09)
//        public int D1 { get; set; }      // 螺栓直径 (mm)
//        public int D { get; set; }       // 图中绘制螺栓直径 (mm)
//        public int V { get; set; }       // 螺帽总长 (mm)
//        public int H1 { get; set; }      // 螺栓长度 (mm)
//        public int H2 { get; set; }      // 栓底间距 (mm)
//        public int E { get; set; }       // 开孔直径 (mm)
//        public int G { get; set; }       // 垫层厚度 (mm)
//        public int A { get; set; }       // 垫板厚度 (mm)
//        public int NutHeight { get; set; } // 开孔深度 (mm)
//        public int BoltLength { get; set; } // 丝长 (mm)
//        public AnchorBolt(string model, int d, int d1, int v, int h1, int h2, int e, int g, int a, int nutHeight, int boltLength)
//        {
//            Model = model;
//            D = d;
//            D1 = d1;
//            V = v;
//            H1 = h1;
//            H2 = h2;
//            E = e;
//            G = g;
//            A = a;
//            NutHeight = nutHeight;
//            BoltLength = boltLength;
//        }
//        public override string ToString()
//        {
//            return $"型号: {Model}\n" +
//                   $"螺栓孔径 (d): {D} mm\n" +
//                   $"螺栓直径 (d1): {D1} mm\n" +
//                   $"螺帽总长 (V): {V} mm\n" +
//                   $"螺栓长度 (h1): {H1} mm\n" +
//                   $"栓底间距 (h2): {H2} mm\n" +
//                   $"开孔直径 (e): {E} mm\n" +
//                   $"垫层厚度 (G): {G} mm\n" +
//                   $"垫板厚度 (d): {A} mm\n" +
//                   $"开孔深度: {NutHeight} mm\n" +
//                   $"丝长: {BoltLength} mm";
//        }
//    }
//    public static class AnchorBoltFactory
//    {
//        public static AnchorBolt CreateBolt(string model)
//        {
//            switch (model)
//            {
//                case "1":
//                    return new AnchorBolt("1", 130, 30, 50, 900, 100, 200, 50, 30, 1000, 80);
//                case "2":
//                    return new AnchorBolt("2", 100, 24, 50, 400, 100, 160, 50, 24, 500, 60);
//                case "3":
//                    return new AnchorBolt("3", 100, 24, 50, 400, 100, 160, 50, 24, 500, 60);
//                case "4":
//                    return new AnchorBolt("4", 100, 24, 50, 500, 100, 160, 50, 24, 600, 60);
//                case "5":
//                    return new AnchorBolt("5", 130, 30, 50, 750, 100, 200, 50, 30, 850, 80);
//                case "6":
//                    return new AnchorBolt("6", 100, 24, 50, 500, 100, 160, 50, 24, 600, 60);
//                case "7":
//                    return new AnchorBolt("7", 80, 20, 50, 400, 100, 160, 50, 20, 500, 60);
//                case "8":
//                    return new AnchorBolt("8", 150, 36, 60, 700, 100, 200, 50, 36, 850, 90);
//                case "9":
//                    return new AnchorBolt("9", 175, 42, 60, 800, 100, 250, 50, 42, 900, 100);
//                default:
//                    throw new ArgumentException("无效的型号！请提供 1-9 之间的型号。");
//            }
//        }
//    }
//    public static partial class HyCommand
//    {
//        // 数据结构定义（使用 ObjectId）
//        public class AxisData
//        {
//            public string Name { get; set; } 
//            public int Number { get; set; } 
//            public List<BaseData> Bases { get; set; } = new List<BaseData>();
//            public List<BoltData> Bolts { get; set; } = new List<BoltData>(); // 未使用
//        }
//        [Serializable]
//        public class BaseData
//        {
//            public string Name { get; set; }
//            public int Number { get; set; }
//            [JsonProperty("Id")]
//            private string IdHandle { get; set; } = null; // 默认值 null
//            [JsonIgnore]
//            public ObjectId Id
//            {
//                get => GetObjectIdFromHandle(IdHandle);
//                set => IdHandle = value.IsNull ? null : value.Handle.ToString();
//            }
//            public List<BoltData> Bolts { get; set; } = new List<BoltData>();
//            private ObjectId GetObjectIdFromHandle(string handle)
//            {
//                if (string.IsNullOrEmpty(handle)) return ObjectId.Null;
//                using (var db = HostApplicationServices.WorkingDatabase)
//                {
//                    if (db != null && db.TryGetObjectId(new Handle(Convert.ToInt64(handle, 16)), out ObjectId id))
//                        return id;
//                    return ObjectId.Null;
//                }
//            }
//        }
//        [Serializable]
//        public class BoltData
//        {
//            // 使用 Handle 存储 ObjectId 的值，供序列化使用
//            [JsonProperty("Id")]
//            private string IdHandle { get; set; }
//            // 提供给 AutoCAD 使用的 ObjectId 属性（不参与序列化）
//            [JsonIgnore]
//            public ObjectId Id
//            {
//                get => GetObjectIdFromHandle(IdHandle);
//                set => IdHandle = value.IsNull ? null : value.Handle.ToString();
//            }
//            public string Model { get; set; } // 型号，与 AnchorBolt 的 Model 对应
//            // 获取对应的 AnchorBolt 对象
//            public AnchorBolt GetAnchorBolt()
//            {
//                return AnchorBoltFactory.CreateBolt(Model);
//            }
//            // 从 Handle 恢复 ObjectId 的方法
//            private ObjectId GetObjectIdFromHandle(string handle)
//            {
//                if (string.IsNullOrEmpty(handle))
//                    return ObjectId.Null;
//                using (var db = HostApplicationServices.WorkingDatabase)
//                {
//                    if (db != null && db.TryGetObjectId(new Handle(Convert.ToInt64(handle, 16)), out ObjectId id))
//                        return id;
//                    return ObjectId.Null;
//                }
//            }
//        }
//        public class BoltEntityData
//        {
//            public string GUID { get; set; } // 螺栓 GUID
//        }
//    }
//}
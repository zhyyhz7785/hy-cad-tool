using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HyCADTool.Models
{
    public class AnchorBolt
    {
        public string Model { get; set; }
        public int D1 { get; set; }
        public int D { get; set; }
        public int V { get; set; }
        public int H1 { get; set; }
        public int H2 { get; set; }
        public int E { get; set; }
        public int G { get; set; }
        public int A { get; set; }
        public int NutHeight { get; set; }
        public int BoltLength { get; set; }

        public AnchorBolt(string model, int d, int d1, int v, int h1, int h2, int e, int g, int a, int nutHeight, int boltLength)
        {
            Model = model;
            D = d;
            D1 = d1;
            V = v;
            H1 = h1;
            H2 = h2;
            E = e;
            G = g;
            A = a;
            NutHeight = nutHeight;
            BoltLength = boltLength;
        }

        public override string ToString()
        {
            return $"型号: {Model}\n" +
                   $"螺栓孔径 (d): {D} mm\n" +
                   $"螺栓直径 (d1): {D1} mm\n" +
                   $"螺帽总长 (V): {V} mm\n" +
                   $"螺栓长度 (h1): {H1} mm\n" +
                   $"栓底间距 (h2): {H2} mm\n" +
                   $"开孔直径 (e): {E} mm\n" +
                   $"垫层厚度 (G): {G} mm\n" +
                   $"垫板厚度 (d): {A} mm\n" +
                   $"开孔深度: {NutHeight} mm\n" +
                   $"丝长: {BoltLength} mm";
        }
    }

    public static class AnchorBoltFactory
    {
        public static AnchorBolt CreateBolt(string model)
        {
            switch (model)
            {
                case "1": return new AnchorBolt("1", 130, 30, 50, 900, 100, 200, 50, 30, 1000, 80);
                case "2": return new AnchorBolt("2", 100, 24, 50, 400, 100, 160, 50, 24, 500, 60);
                case "3": return new AnchorBolt("3", 100, 24, 50, 400, 100, 160, 50, 24, 500, 60);
                case "4": return new AnchorBolt("4", 100, 24, 50, 500, 100, 160, 50, 24, 600, 60);
                case "5": return new AnchorBolt("5", 130, 30, 50, 750, 100, 200, 50, 30, 850, 80);
                case "6": return new AnchorBolt("6", 100, 24, 50, 500, 100, 160, 50, 24, 600, 60);
                case "7": return new AnchorBolt("7", 80, 20, 50, 400, 100, 160, 50, 20, 500, 60);
                case "8": return new AnchorBolt("8", 150, 36, 60, 700, 100, 200, 50, 36, 850, 90);
                case "9": return new AnchorBolt("9", 175, 42, 60, 800, 100, 250, 50, 42, 900, 100);
                default: throw new ArgumentException("无效的型号！请提供 1-9 之间的型号。");
            }
        }
    }

    [Serializable]
    public class AxisData
    {
        public string Name { get; set; }
        public int SerialNumber { get; set; }
        public List<BaseData> Bases { get; set; } = new List<BaseData>();
        [JsonIgnore]
        public Dictionary<Guid, ObjectId> EntityIdMap { get; set; } = new Dictionary<Guid, ObjectId>(); // 运行时缓存
        public (double X, double Y) IntersectionPoint { get; set; }

        public void RebuildEntityIdMap(Database db, Transaction tr, Editor ed)
        {
            EntityIdMap.Clear();
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

            foreach (ObjectId objId in btr)
            {
                var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (entity != null)
                {
                    Guid guid = ExtensionDictionaryUtils.ReadGuidFromExtensionDictionary(tr, entity, ed);
                    if (guid != Guid.Empty)
                    {
                        EntityIdMap[guid] = objId;
                    }
                }
            }
        }
    }

    [Serializable]
    public class BaseData
    {
        [JsonProperty("Id")]
        public Guid Id { get; set; }
        public int SerialNumber { get; set; }
        [JsonProperty("BoltIds")]
        public List<Guid> BoltIds { get; set; } = new List<Guid>();

        [JsonConstructor]
        public BaseData()
        {
            Id = Guid.NewGuid();
        }

        public void AddBoltId(Guid boltId)
        {
            BoltIds.Add(boltId);
        }

        public Polyline GetPolyline(Database db, Transaction tr, Editor ed)
        {
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
            foreach (ObjectId objId in btr)
            {
                if (tr.GetObject(objId, OpenMode.ForRead) is Polyline polyline)
                {
                    Guid polylineGuid = ExtensionDictionaryUtils.ReadGuidFromExtensionDictionary(tr, polyline, ed);
                    if (polylineGuid == Id)
                    {
                        return polyline;
                    }
                }
            }
            return null;
        }

        public Circle GetCircle(Database db, Transaction tr, Editor ed, Guid boltId)
        {
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
            foreach (ObjectId objId in btr)
            {
                if (tr.GetObject(objId, OpenMode.ForRead) is Circle circle)
                {
                    Guid circleGuid = ExtensionDictionaryUtils.ReadGuidFromExtensionDictionary(tr, circle, ed);
                    if (circleGuid == boltId)
                    {
                        return circle;
                    }
                }
            }
            return null;
        }
    }

    [Serializable]
    public class BoltData
    {
        [JsonProperty("Id")]
        public Guid Id { get; set; }
        public string Model { get; set; }

        public AnchorBolt GetAnchorBolt()
        {
            return AnchorBoltFactory.CreateBolt(Model);
        }

        public BoltData()
        {
            Id = Guid.NewGuid();
        }

        public Circle GetCircle(Database db, Transaction tr, Editor ed)
        {
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
            foreach (ObjectId objId in btr)
            {
                if (tr.GetObject(objId, OpenMode.ForRead) is Circle circle)
                {
                    Guid circleGuid = ExtensionDictionaryUtils.ReadGuidFromExtensionDictionary(tr, circle, ed);
                    if (circleGuid == Id)
                    {
                        return circle;
                    }
                }
            }
            return null;
        }
    }

    [Serializable]
    public class EquipmentData
    {
        public int Number { get; set; }
        public string EquipmentName { get; set; }
        public double Weight { get; set; }
        public double LateralForce { get; set; }
        public double HorizontalForce { get; set; }
        public double VerticalForce { get; set; }

        public EquipmentData(int number, string equipmentName, double weight, double lateralForce, double horizontalForce, double verticalForce)
        {
            Number = number;
            EquipmentName = equipmentName;
            Weight = weight;
            LateralForce = lateralForce;
            HorizontalForce = horizontalForce;
            VerticalForce = verticalForce;
        }

        public EquipmentData() { }
    }

    public class EquipmentDataManager
    {
        private readonly List<EquipmentData> _equipmentList;
        private readonly string _filePath = @"E:\BaiduSyncdisk\Code\testResult\00equipment_data.md";

        public EquipmentDataManager()
        {
            _equipmentList = new List<EquipmentData>();
            LoadFromMarkdown(_filePath);
        }

        public void LoadFromMarkdown(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("Markdown file not found.", filePath);

            _equipmentList.Clear();
            var lines = File.ReadAllLines(filePath);
            for (int i = 2; i < lines.Length; i++)
            {
                var columns = lines[i].Split('|').Select(col => col.Trim()).ToArray();
                if (columns.Length < 7) continue;

                if (int.TryParse(columns[1], out int number) &&
                    double.TryParse(columns[3], out double weight) &&
                    double.TryParse(columns[4], out double lateralForce) &&
                    double.TryParse(columns[5], out double horizontalForce) &&
                    double.TryParse(columns[6], out double verticalForce))
                {
                    _equipmentList.Add(new EquipmentData(number, columns[2], weight, lateralForce, horizontalForce, verticalForce));
                }
            }
        }

        public void SaveToMarkdown(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| 序号 | 设备名称 | 设备重量G (kg) | 横向力矩G (kgM) | 水平力G (kg) | 垂直力G (kg) |");
            sb.AppendLine("|------|----------|---------------|----------------|-------------|-------------|");

            foreach (var equipment in _equipmentList.OrderBy(e => e.Number))
            {
                sb.AppendLine($"| {equipment.Number} | {equipment.EquipmentName} | {equipment.Weight} | {equipment.LateralForce} | {equipment.HorizontalForce} | {equipment.VerticalForce} |");
            }

            File.WriteAllText(filePath, sb.ToString());
        }

        public EquipmentData GetEquipmentByNumber(int number)
        {
            return _equipmentList.FirstOrDefault(e => e.Number == number);
        }

        public List<EquipmentData> GetAllEquipment()
        {
            return _equipmentList.ToList();
        }
    }
}
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HyCADTool.Refactored.Domain.Entities
{
    /// <summary>
    /// 设备基础底座数据
    /// 关联 Polyline（轮廓）与多个 Bolt（螺栓圆）
    /// </summary>
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
    }

    /// <summary>
    /// 螺栓数据（Circle 上附加的扩展字典）
    /// </summary>
    [Serializable]
    public class BoltData
    {
        [JsonProperty("Id")]
        public Guid Id { get; set; }

        public string Model { get; set; }

        public BoltData()
        {
            Id = Guid.NewGuid();
        }

        public AnchorBolt GetAnchorBolt()
        {
            return AnchorBoltFactory.CreateBolt(Model);
        }
    }

    /// <summary>
    /// 轴线数据（Line 上附加的扩展字典）
    /// 包含轴线名称、序号、关联底座和交点坐标
    /// </summary>
    [Serializable]
    public class AxisData
    {
        public string Name { get; set; }
        public int SerialNumber { get; set; }
        public List<BaseData> Bases { get; set; } = new List<BaseData>();
        public (double X, double Y) IntersectionPoint { get; set; }
    }

    /// <summary>
    /// 设备荷载数据
    /// </summary>
    [Serializable]
    public class EquipmentData
    {
        public int Number { get; set; }
        public string EquipmentName { get; set; }
        public double Weight { get; set; }
        public double LateralForce { get; set; }
        public double HorizontalForce { get; set; }
        public double VerticalForce { get; set; }

        public EquipmentData() { }

        public EquipmentData(int number, string equipmentName, double weight,
            double lateralForce, double horizontalForce, double verticalForce)
        {
            Number = number;
            EquipmentName = equipmentName;
            Weight = weight;
            LateralForce = lateralForce;
            HorizontalForce = horizontalForce;
            VerticalForce = verticalForce;
        }
    }

    /// <summary>
    /// 设备数据管理器
    /// 从 Markdown 表格文件读取/写入设备荷载数据
    /// </summary>
    public class EquipmentDataManager
    {
        private readonly List<EquipmentData> _equipmentList = new List<EquipmentData>();

        public EquipmentDataManager(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                LoadFromMarkdown(filePath);
            }
        }

        public void LoadFromMarkdown(string filePath)
        {
            if (!File.Exists(filePath))
                throw new System.Exception($"设备数据文件未找到: {filePath}");

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
                    _equipmentList.Add(new EquipmentData(number, columns[2], weight,
                        lateralForce, horizontalForce, verticalForce));
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

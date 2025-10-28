using System;
using System.IO;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Domain.ValueObjects
{
    /// <summary>
    /// 容差管理类（单例模式）
    /// 统一管理所有几何计算中的容差值
    /// </summary>
    public sealed class ToleranceSettings
    {
        private static readonly Lazy<ToleranceSettings> _instance = 
            new Lazy<ToleranceSettings>(() => new ToleranceSettings());
        
        public static ToleranceSettings Instance => _instance.Value;
        
        // ==================== 几何容差 ====================
        
        /// <summary>
        /// 距离容差（mm）
        /// 用于：点、线距离比较
        /// </summary>
        public double DistanceTolerance { get; set; } = 0.001;
        
        /// <summary>
        /// 角度容差（弧度）
        /// 用于：墙体夹角计算
        /// </summary>
        public double AngleTolerance { get; set; } = 0.0001;
        
        /// <summary>
        /// 面积容差（mm²）
        /// 用于：多边形面积验证
        /// </summary>
        public double AreaTolerance { get; set; } = 0.01;
        
        // ==================== 标高容差 ====================
        
        /// <summary>
        /// 标高差容差（mm）
        /// 用于：标高差判断（是否为墙体）
        /// </summary>
        public double ElevationTolerance { get; set; } = 1.0;
        
        // ==================== 墙体容差 ====================
        
        /// <summary>
        /// 墙体厚度容差（mm）
        /// 用于：墙体厚度验证
        /// </summary>
        public double ThicknessTolerance { get; set; } = 0.5;
        
        /// <summary>
        /// 边重合判断容差（mm）
        /// 用于：边重合检测（相邻多边形）
        /// </summary>
        public double EdgeCoincidenceTolerance { get; set; } = 0.1;
        
        /// <summary>
        /// 点在多边形内判断容差（mm）
        /// 用于：标高文本匹配到多边形
        /// </summary>
        public double PointInPolygonTolerance { get; set; } = 0.01;
        
        // ==================== Clipper2 配置 ====================
        
        /// <summary>
        /// Clipper2 缩放因子
        /// 默认：100,000（10^5）
        /// 高精度：1,000,000（10^6）
        /// </summary>
        public int Clipper2ScaleFactor { get; set; } = 100000;
        
        // ==================== 墙体连接配置 ====================
        
        /// <summary>
        /// Round 连接的圆弧段数
        /// 默认：16（平衡精度和性能）
        /// </summary>
        public int RoundConnectionSegments { get; set; } = 16;
        
        /// <summary>
        /// 圆角缓冲区的圆弧段数
        /// 默认：16
        /// </summary>
        public int RoundedBufferSegments { get; set; } = 16;
        
        private ToleranceSettings() { }
        
        /// <summary>
        /// 从配置文件加载容差设置
        /// </summary>
        public void LoadFromConfig(string configPath)
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    SaveToConfig(configPath); // 创建默认配置文件
                    return;
                }
                
                string json = File.ReadAllText(configPath);
                var settings = JsonConvert.DeserializeObject<ToleranceSettings>(json);
                
                if (settings != null)
                {
                    // 复制所有属性
                    DistanceTolerance = settings.DistanceTolerance;
                    AngleTolerance = settings.AngleTolerance;
                    AreaTolerance = settings.AreaTolerance;
                    ElevationTolerance = settings.ElevationTolerance;
                    ThicknessTolerance = settings.ThicknessTolerance;
                    EdgeCoincidenceTolerance = settings.EdgeCoincidenceTolerance;
                    PointInPolygonTolerance = settings.PointInPolygonTolerance;
                    Clipper2ScaleFactor = settings.Clipper2ScaleFactor;
                    RoundConnectionSegments = settings.RoundConnectionSegments;
                    RoundedBufferSegments = settings.RoundedBufferSegments;
                }
            }
            catch (Exception ex)
            {
                // 加载失败，使用默认值
                System.Diagnostics.Debug.WriteLine($"加载容差配置失败：{ex.Message}");
            }
        }
        
        /// <summary>
        /// 保存容差设置到配置文件
        /// </summary>
        public void SaveToConfig(string configPath)
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存容差配置失败：{ex.Message}");
            }
        }
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefaults()
        {
            DistanceTolerance = 0.001;
            AngleTolerance = 0.0001;
            AreaTolerance = 0.01;
            ElevationTolerance = 1.0;
            ThicknessTolerance = 0.5;
            EdgeCoincidenceTolerance = 0.1;
            PointInPolygonTolerance = 0.01;
            Clipper2ScaleFactor = 100000;
            RoundConnectionSegments = 16;
            RoundedBufferSegments = 16;
        }
    }
}















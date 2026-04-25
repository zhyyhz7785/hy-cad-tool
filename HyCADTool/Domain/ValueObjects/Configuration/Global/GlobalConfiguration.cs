using System;

namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    /// <summary>
    /// 全局配置值对象
    /// 包含所有全局级别的配置项，影响整个应用程序
    /// </summary>
    public class GlobalConfiguration
    {
        /// <summary>
        /// 比例配置
        /// </summary>
        public ScaleConfig Scale { get; set; }

        /// <summary>
        /// 容差配置
        /// </summary>
        public ToleranceConfig Tolerance { get; set; }

        /// <summary>
        /// 路径配置
        /// </summary>
        public PathConfig Paths { get; set; }

        /// <summary>
        /// 样式配置集合
        /// </summary>
        public StylesConfig Styles { get; set; }

        /// <summary>
        /// 标高符号长度（兼容原 BaseConfig.ElevationLength）
        /// </summary>
        public double ElevationLength { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public GlobalConfiguration()
        {
            Scale = ScaleConfig.CreateDefault();
            Tolerance = ToleranceConfig.CreateDefault();
            Paths = PathConfig.CreateDefault();
            Styles = StylesConfig.CreateDefault();
            ElevationLength = 2.0;
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid(out string error)
        {
            if (Scale == null)
            {
                error = "比例配置不能为空";
                return false;
            }

            if (!Scale.IsValid(out error))
                return false;

            if (Tolerance == null)
            {
                error = "容差配置不能为空";
                return false;
            }

            if (!Tolerance.IsValid(out error))
                return false;

            if (Paths == null)
            {
                error = "路径配置不能为空";
                return false;
            }

            if (!Paths.IsValid(out error))
                return false;

            if (Styles == null)
            {
                error = "样式配置不能为空";
                return false;
            }

            if (!Styles.IsValid(out error))
                return false;

            if (ElevationLength <= 0)
            {
                error = "标高长度必须大于 0";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static GlobalConfiguration CreateDefault()
        {
            var config = new GlobalConfiguration
            {
                Scale = ScaleConfig.CreateDefault(),
                Tolerance = ToleranceConfig.CreateDefault(),
                Paths = PathConfig.CreateDefault(),
                Styles = StylesConfig.CreateDefault(40.0),
                ElevationLength = 2.0
            };

            return config;
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public GlobalConfiguration Clone()
        {
            // 深拷贝所有配置
            return new GlobalConfiguration
            {
                Scale = new ScaleConfig
                {
                    Default = this.Scale.Default,
                    MinValue = this.Scale.MinValue,
                    MaxValue = this.Scale.MaxValue
                },
                Tolerance = new ToleranceConfig
                {
                    Double = this.Tolerance.Double,
                    Vector = this.Tolerance.Vector,
                    Point = this.Tolerance.Point
                },
                Paths = new PathConfig
                {
                    DefaultExportPath = this.Paths.DefaultExportPath,
                    DefaultImportPath = this.Paths.DefaultImportPath,
                    TempFilesPath = this.Paths.TempFilesPath,
                    ConfigDirectory = this.Paths.ConfigDirectory
                },
                Styles = new StylesConfig
                {
                    TextStyle = this.Styles.TextStyle,
                    DimensionStyle = this.Styles.DimensionStyle,
                    MLeaderStyle = this.Styles.MLeaderStyle
                },
                ElevationLength = this.ElevationLength
            };
        }

        public override string ToString()
        {
            return $"GlobalConfiguration[Scale={Scale.Default}, Tolerance={Tolerance.Double:E2}]";
        }
    }
}


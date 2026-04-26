using System;

namespace HyCADTool.Domain.Entities
{
    /// <summary>
    /// 地脚螺栓实体
    /// 包含螺栓的全部规格参数（型号1-9）
    /// </summary>
    [Serializable]
    public class AnchorBolt
    {
        public string Model { get; set; }
        /// <summary>螺栓孔径 (mm)</summary>
        public int D { get; set; }
        /// <summary>螺栓直径 (mm)</summary>
        public int D1 { get; set; }
        /// <summary>螺帽总长 (mm)</summary>
        public int V { get; set; }
        /// <summary>螺栓长度 (mm)</summary>
        public int H1 { get; set; }
        /// <summary>栓底间距 (mm)</summary>
        public int H2 { get; set; }
        /// <summary>开孔直径 (mm)</summary>
        public int E { get; set; }
        /// <summary>垫层厚度 (mm)</summary>
        public int G { get; set; }
        /// <summary>垫板厚度 (mm)</summary>
        public int A { get; set; }
        /// <summary>开孔深度 (mm)</summary>
        public int NutHeight { get; set; }
        /// <summary>丝长 (mm)</summary>
        public int BoltLength { get; set; }

        public AnchorBolt() { }

        public AnchorBolt(string model, int d, int d1, int v, int h1, int h2, int e, int g, int a, int nutHeight, int boltLength)
        {
            Model = model; D = d; D1 = d1; V = v; H1 = h1; H2 = h2;
            E = e; G = g; A = a; NutHeight = nutHeight; BoltLength = boltLength;
        }

        public override string ToString()
        {
            return $"AnchorBolt[Model={Model}, D={D}, D1={D1}, H1={H1}, H2={H2}]";
        }
    }

    /// <summary>
    /// 地脚螺栓工厂（预定义型号1-9）
    /// </summary>
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
                default: throw new ArgumentException($"无效的螺栓型号: {model}，请提供 1-9 之间的型号。");
            }
        }
    }
}

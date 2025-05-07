using NetTopologySuite.Geometries;
using System;
namespace HyCADTool.HelpClass.CreatBase
{
    public class AnchorBolt
    {
        public string Model { get; set; } // 型号 (01-09)
        public int D1 { get; set; }      // 螺栓直径 (mm)
        public int V { get; set; }       // 螺帽总长 (mm)
        public int H1 { get; set; }      // 螺栓长度 (mm)
        public int H2 { get; set; }      // 栓底间距 (mm)
        public int E { get; set; }       // 开孔直径 (mm)
        public int G { get; set; }       // 垫层厚度 (mm)
        public int A { get; set; }       // 垫板厚度 (mm)
        public int NutHeight { get; set; } // 开孔深度 (mm)
        public int BoltLength { get; set; } // 丝长 (mm)
        public AnchorBolt(string model, int d1, int v, int h1, int h2, int e, int g, int a, int nutHeight, int boltLength)
        {
            Model = model;
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
                case "1":
                    return new AnchorBolt("1", 30, 50, 900, 100, 200, 50, 30, 1000, 80);
                case "2":
                    return new AnchorBolt("2", 24, 50, 400, 100, 160, 50, 24, 500, 60);
                case "3":
                    return new AnchorBolt("3", 24, 50, 400, 100, 160, 50, 24, 500, 60);
                case "4":
                    return new AnchorBolt("4", 24, 50, 500, 100, 160, 50, 24, 600, 60);
                case "5":
                    return new AnchorBolt("5", 30, 50, 750, 100, 200, 50, 30, 850, 80);
                case "6":
                    return new AnchorBolt("6", 24, 50, 500, 100, 160, 50, 24, 600, 60);
                case "7":
                    return new AnchorBolt("7", 20, 50, 400, 100, 160, 50, 20, 500, 60);
                case "8":
                    return new AnchorBolt("8", 36, 60, 700, 100, 200, 50, 36, 850, 90);
                case "9":
                    return new AnchorBolt("9", 42, 60, 800, 100, 250, 50, 42, 900, 100);
                default:
                    throw new ArgumentException("无效的型号！请提供 1-9 之间的型号。");
            }
        }
    }
}
using System;
using System.IO;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Repository
{
    /// <summary>
    /// hyob 仓库路径定位。给一个 .dwg 路径，返回与之平行的 .hyob/ 目录布局。
    /// 设计：02 §4 / 04 §4。
    ///
    /// <code>
    ///   <DWG-FILE>.dwg
    ///   <DWG-FILE>.hyob/
    ///     HEAD
    ///     objects/<hh>/<hex[2..]>
    ///     refs/heads/main
    ///     log/operations.jsonl
    /// </code>
    /// </summary>
    public sealed class HyobLayoutPaths
    {
        public string DwgPath { get; }
        public string HyobRoot { get; }

        public string ObjectsDir => Path.Combine(HyobRoot, "objects");
        public string RefsDir    => Path.Combine(HyobRoot, "refs");
        public string LogDir     => Path.Combine(HyobRoot, "log");
        public string ExportsDir => Path.Combine(HyobRoot, "exports");
        public string OperationsLogPath => Path.Combine(LogDir, "operations.jsonl");

        public HyobLayoutPaths(string dwgPath)
        {
            if (string.IsNullOrEmpty(dwgPath))
                throw new ArgumentException("dwgPath 不能为空", nameof(dwgPath));
            DwgPath = dwgPath;
            HyobRoot = dwgPath + ".hyob";
        }

        public bool Exists() => Directory.Exists(HyobRoot);

        public void EnsureCreated()
        {
            Directory.CreateDirectory(HyobRoot);
            Directory.CreateDirectory(ObjectsDir);
            Directory.CreateDirectory(LogDir);
        }
    }
}

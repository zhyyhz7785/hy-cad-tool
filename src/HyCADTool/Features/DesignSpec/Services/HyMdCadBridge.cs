using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.DesignSpec.Services
{
    /// <summary>
    /// AutoCAD → HyMd 命名管道客户端。HyMd 为 server（\\.\pipe\HyMd.Cad.v1，NDJSON）。
    /// </summary>
    public static class HyMdCadBridge
    {
        public const string PipeShortName = "HyMd.Cad.v1";

        private static readonly object Gate = new object();
        private static readonly ConcurrentQueue<Action> IdleActions = new ConcurrentQueue<Action>();
        private static readonly string[] AllowedServerProcessNames = { "HyMd", "hymd-tauri2", "hymd" };
        private static NamedPipeClientStream _pipe;
        private static StreamReader _reader;
        private static StreamWriter _writer;
        private static Thread _readThread;
        private static volatile bool _stop;
        private static bool _stampHooked;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

        public static bool IsConnected
        {
            get
            {
                lock (Gate)
                    return _pipe != null && _pipe.IsConnected;
            }
        }

        public static double LastScale { get; private set; } = 1.0;
        public static string LastStyleName { get; private set; } = "0-hy-说明-T";

        public static event Action<CadStampRequest> StampRequested;

        public static void Start()
        {
            EnsureStampHook();
            TryConnect(300);
            if (IsConnected)
                SendHello();
        }

        public static void Stop()
        {
            _stop = true;
            lock (Gate)
            {
                try { _writer?.Dispose(); } catch { }
                try { _reader?.Dispose(); } catch { }
                try { _pipe?.Dispose(); } catch { }
                _writer = null;
                _reader = null;
                _pipe = null;
            }
        }

        public static bool EnsureProcess(Editor ed)
        {
            EnsureStampHook();
            if (IsConnected)
                return true;

            if (TryConnect(400))
            {
                SendHello();
                return true;
            }

            string exe = HyMdCadSettings.ResolveExePath();
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            {
                ed?.WriteMessage("\n未找到 HyMd 可执行文件，请选择 HyMd.exe 或 hymd-tauri2.exe。");
                exe = HyMdCadSettings.PromptForExe();
            }

            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            {
                ed?.WriteMessage("\n已取消：没有可用的 HyMd。");
                return false;
            }

            if (!IsHyMdProcessRunning())
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exe,
                        UseShellExecute = true
                    });
                    ed?.WriteMessage("\n正在启动 HyMd…");
                }
                catch (Exception ex)
                {
                    ed?.WriteMessage("\n启动 HyMd 失败: " + ex.Message);
                    return false;
                }
            }

            if (!TryConnect(12000))
            {
                ed?.WriteMessage("\n无法连接 HyMd 管道（HyMd.Cad.v1）。请确认 HyMd 已启动。");
                return false;
            }

            SendHello();
            return true;
        }

        public static void SendBindAndContext(string dwgPath, double scale, string styleName)
        {
            if (scale > 0) LastScale = scale;
            if (!string.IsNullOrWhiteSpace(styleName)) LastStyleName = styleName;

            WriteLine(new JObject
            {
                ["v"] = 1,
                ["op"] = "bind",
                ["dwgPath"] = dwgPath ?? "",
                ["scale"] = LastScale,
                ["styleName"] = LastStyleName ?? "0-hy-说明-T"
            });
            WriteLine(new JObject
            {
                ["v"] = 1,
                ["op"] = "context",
                ["dwgPath"] = dwgPath ?? "",
                ["scale"] = LastScale,
                ["styleName"] = LastStyleName ?? "0-hy-说明-T"
            });
        }


        public static void SendOpen(string sourcePath, string markdown, string dwgPath, double scale, string styleName)
        {
            SendBindAndContext(dwgPath, scale, styleName);
            WriteLine(new JObject
            {
                ["v"] = 1,
                ["op"] = "open",
                ["path"] = sourcePath ?? "",
                ["markdown"] = markdown ?? "",
                ["dwgPath"] = dwgPath ?? "",
                ["scale"] = LastScale,
                ["styleName"] = LastStyleName ?? "0-hy-说明-T"
            });
        }

        public static void SendJson(JObject obj)
        {
            if (obj == null)
                return;
            WriteLine(obj);
        }

        private static void SendHello()
        {
            WriteLine(new JObject
            {
                ["v"] = 1,
                ["op"] = "hello"
            });
        }

        private static void WriteLine(JObject obj)
        {
            lock (Gate)
            {
                if (_writer == null || _pipe == null || !_pipe.IsConnected)
                    return;
                _writer.WriteLine(obj.ToString(Formatting.None));
                _writer.Flush();
            }
        }

        private static bool TryConnect(int timeoutMs)
        {
            lock (Gate)
            {
                if (_pipe != null && _pipe.IsConnected)
                    return true;
                CloseUnlocked();
            }

            var pipe = new NamedPipeClientStream(".", PipeShortName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                pipe.Connect(Math.Max(50, timeoutMs));
                if (!TryValidatePipeServer(pipe))
                {
                    try { pipe.Dispose(); } catch { }
                    return false;
                }
            }
            catch
            {
                try { pipe.Dispose(); } catch { }
                return false;
            }

            lock (Gate)
            {
                CloseUnlocked();
                _stop = false;
                _pipe = pipe;
                var utf8 = new UTF8Encoding(false);
                _reader = new StreamReader(pipe, utf8, false, 4096, true);
                _writer = new StreamWriter(pipe, utf8, 4096, true) { AutoFlush = true };
                _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "HyMdCadPipe" };
                _readThread.Start();
            }

            return true;
        }

        private static void CloseUnlocked()
        {
            try { _writer?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _pipe?.Dispose(); } catch { }
            _writer = null;
            _reader = null;
            _pipe = null;
        }

        private static void ReadLoop()
        {
            StreamReader reader;
            lock (Gate) reader = _reader;
            if (reader == null) return;

            try
            {
                while (!_stop)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                        break;
                    HandleLine(line);
                }
            }
            catch
            {
            }
            finally
            {
                lock (Gate)
                {
                    if (ReferenceEquals(_reader, reader))
                        CloseUnlocked();
                }
            }
        }

        private static void HandleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;
            JObject obj;
            try
            {
                obj = JObject.Parse(line);
            }
            catch
            {
                return;
            }

            string op = obj.Value<string>("op") ?? "";
            if (string.Equals(op, "hymdcFile", StringComparison.OrdinalIgnoreCase))
            {
                string path = obj.Value<string>("path") ?? "";
                QueueOnIdle(() =>
                {
                    try
                    {
                        AcApp.DocumentManager.MdiActiveDocument?.Editor
                            .WriteMessage("\nHyMd 已写入 " + path);
                    }
                    catch { }
                });
                return;
            }
            if (!string.Equals(op, "stamp", StringComparison.OrdinalIgnoreCase))
                return;

            CadStampPlan plan = null;
            var planToken = obj["plan"];
            if (planToken != null && planToken.Type != JTokenType.Null)
            {
                try { plan = planToken.ToObject<CadStampPlan>(); }
                catch { plan = null; }
            }

            CadLayoutDump dump = null;
            var dumpToken = obj["dump"];
            if (dumpToken != null && dumpToken.Type != JTokenType.Null)
            {
                try { dump = dumpToken.ToObject<CadLayoutDump>(); }
                catch { dump = null; }
            }

            if (plan == null && dump == null)
                return;

            var handler = StampRequested;
            if (handler == null)
                return;
            handler(new CadStampRequest { Dump = dump, Plan = plan });
        }

        private static bool IsHyMdProcessRunning()
        {
            try
            {
                foreach (var name in new[] { "HyMd", "hymd-tauri2", "hymd" })
                {
                    if (Process.GetProcessesByName(name).Length > 0)
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryValidatePipeServer(NamedPipeClientStream pipe)
        {
            try
            {
                if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint pid))
                    return false;
                using (var proc = Process.GetProcessById(unchecked((int)pid)))
                {
                    string name = proc.ProcessName ?? string.Empty;
                    foreach (var allowed in AllowedServerProcessNames)
                    {
                        if (string.Equals(name, allowed, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static void EnsureStampHook()
        {
            if (_stampHooked) return;
            _stampHooked = true;
            StampRequested += req => QueueOnIdle(() => HyMdCadStampHandler.Run(req));
            AcApp.Idle += DrainIdleQueue;
        }

        private static void DrainIdleQueue(object sender, EventArgs e)
        {
            int budget = 16;
            while (budget-- > 0 && IdleActions.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex)
                {
                    try
                    {
                        AcApp.DocumentManager.MdiActiveDocument?.Editor
                            .WriteMessage("\nHyMd 贴图失败: " + ex.Message);
                    }
                    catch { }
                }
            }
        }

        private static void QueueOnIdle(Action action)
        {
            if (action == null)
                return;
            IdleActions.Enqueue(action);
        }
    }

    /// <summary>
    /// 在 AutoCAD 空闲时取点并插入（管道读线程不能直接 GetPoint）。
    /// </summary>
    public static class HyMdCadStampHandler
    {
        public static void Run(CadStampRequest req)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || req == null)
                return;

            var ed = doc.Editor;
            var ptRes = ed.GetPoint("\n请选择设计说明左上角插入点：");
            if (ptRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n已取消插入点选择，本次未贴图。");
                return;
            }

            var service = new DesignSpecService();
            if (req.Dump != null)
            {
                service.InsertFromHyMdDump(
                    req.Dump,
                    ptRes.Value,
                    HyMdCadBridge.LastScale,
                    HyMdCadBridge.LastStyleName);
                ReportPlanDiff(ed, req);
                return;
            }
            if (req.Plan != null && req.Plan.Pages != null)
            {
                service.AppendFromCadStampPlan(req.Plan, ptRes.Value);
                return;
            }
            ed.WriteMessage("\n贴图清单与 dump 都为空，本次未贴图。");
        }

        /// <summary>
        /// 旁路对照：不改图纸。把 HyMd 编的 plan 与 InsertFromHyMdDump 公式编出的基准比一遍。
        /// </summary>
        private static void ReportPlanDiff(Editor ed, CadStampRequest req)
        {
            if (ed == null || req?.Plan == null || req.Dump == null)
                return;
            try
            {
                var baseline = DesignSpecService.BuildStampPlanFromDump(req.Dump, HyMdCadBridge.LastScale);
                var diffs = CadStampPlanGolden.Diff(baseline, req.Plan);
                ed.WriteMessage("\n[HyMd] plan 对照：" + CadStampPlanGolden.FormatDiffs(diffs));
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n[HyMd] plan 对照失败: " + ex.Message);
            }
        }
    }
}

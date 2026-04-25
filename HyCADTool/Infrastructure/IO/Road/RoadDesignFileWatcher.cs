using System;
using System.IO;

namespace HyCADTool.Infrastructure.IO.Road
{
    /// <summary>
    /// <c>.roaddesign.json</c> 文件变化监听器（v1 骨架 / v2 启用）。
    ///
    /// 预留用途（对应 04Pipeline_CAD_Blender_Lumion.md v2 阶段）：
    /// - Blender 插件更新 JSON 后，AutoCAD 侧通过此 watcher 感知变化，重新加载到 RoadDesign 聚合根；
    /// - 配合命令收尾同步 <c>RoadJsonExportService.SaveForDocument</c> 形成"文件为单一事实来源"的双端协作。
    ///
    /// v1 阶段：
    /// - 代码存在但 <b>不由 PluginInitializer 调用 <see cref="Start"/></b>；
    /// - 仅供开发 / 测试时手动启用，避免 v1 写入自身造成"脏事件循环"。
    /// </summary>
    public sealed class RoadDesignFileWatcher : IDisposable
    {
        private FileSystemWatcher _watcher;
        private string _watchedPath;

        /// <summary>
        /// 当监听文件发生变化时触发。订阅方需自行处理去重 / 节流。
        /// </summary>
        public event EventHandler<RoadFileChangedEventArgs> Changed;

        /// <summary>
        /// 开始监听指定 JSON 文件。重复调用会先关闭旧 watcher。
        /// </summary>
        public void Start(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath)) throw new ArgumentException("jsonPath", nameof(jsonPath));

            Stop();

            string dir = Path.GetDirectoryName(jsonPath);
            string fileName = Path.GetFileName(jsonPath);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(fileName))
                throw new ArgumentException("jsonPath 必须包含目录与文件名", nameof(jsonPath));

            _watchedPath = jsonPath;
            _watcher = new FileSystemWatcher(dir, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };
            _watcher.Changed += (s, e) => RaiseChanged(e.FullPath);
            _watcher.Created += (s, e) => RaiseChanged(e.FullPath);
            _watcher.Renamed += (s, e) => RaiseChanged(e.FullPath);
        }

        private void RaiseChanged(string path)
        {
            var handler = Changed;
            if (handler == null) return;
            try
            {
                handler(this, new RoadFileChangedEventArgs(path));
            }
            catch
            {
                // 订阅者异常不影响 Watcher。
            }
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
            _watchedPath = null;
        }

        public string WatchedPath => _watchedPath;

        public void Dispose() => Stop();
    }

    /// <summary>
    /// 文件变化事件参数。
    /// </summary>
    public sealed class RoadFileChangedEventArgs : EventArgs
    {
        public string FullPath { get; }
        public DateTime TimestampUtc { get; }

        public RoadFileChangedEventArgs(string fullPath)
        {
            FullPath = fullPath;
            TimestampUtc = DateTime.UtcNow;
        }
    }
}

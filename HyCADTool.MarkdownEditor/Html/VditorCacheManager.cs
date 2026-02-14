using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HyCADTool.MarkdownEditor.Html
{
    /// <summary>
    /// Vditor 本地缓存管理器
    /// 
    /// 首次启动时自动从 npm 镜像下载 Vditor 包并解压到本地缓存。
    /// 后续启动直接使用本地文件（通过 WebView2 虚拟主机映射），零网络依赖。
    /// 
    /// 缓存位置: %LocalAppData%/HyCADTool/VditorDist/{VERSION}/dist/
    /// </summary>
    internal static class VditorCacheManager
    {
        private static readonly SemaphoreSlim CacheLock = new SemaphoreSlim(1, 1);
        public const string VERSION = "3.10.8";

        /// <summary>CDN 回退地址（本地缓存不可用时使用）</summary>
        public const string CdnFallback = "https://cdn.jsdelivr.net/npm/vditor@" + VERSION;

        /// <summary>下载源列表（国内镜像优先）</summary>
        private static readonly string[] TarballUrls =
        {
            $"https://registry.npmmirror.com/vditor/-/vditor-{VERSION}.tgz",  // 淘宝镜像（国内快）
            $"https://registry.npmjs.org/vditor/-/vditor-{VERSION}.tgz",      // npm 官方（全球）
        };

        /// <summary>缓存根目录</summary>
        public static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HyCADTool", "VditorDist", VERSION);

        /// <summary>dist/ 目录路径</summary>
        public static string DistDir => Path.Combine(CacheDir, "dist");

        /// <summary>本地缓存是否已就绪</summary>
        public static bool IsCached =>
            File.Exists(Path.Combine(DistDir, "index.min.js"))
            && File.Exists(Path.Combine(DistDir, "index.css"));

        /// <summary>
        /// 确保 Vditor 已缓存到本地。
        /// 如果未缓存，从 npm 镜像下载 tarball 并解压 dist/ 目录。
        /// </summary>
        /// <param name="onProgress">进度回调（UI 线程安全由调用方保证）</param>
        public static async Task EnsureCachedAsync(Action<string> onProgress = null)
        {
            if (IsCached)
            {
                onProgress?.Invoke("编辑器资源已就绪");
                return;
            }

            await CacheLock.WaitAsync();
            try
            {
                if (IsCached)
                {
                    onProgress?.Invoke("编辑器资源已就绪");
                    return;
                }

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(120);

                foreach (var url in TarballUrls)
                {
                    try
                    {
                        string host = new Uri(url).Host;
                        onProgress?.Invoke($"首次使用，正在下载编辑器资源 ({host})...");

                        var data = await client.GetByteArrayAsync(url);

                        onProgress?.Invoke("正在解压编辑器资源...");
                        ExtractDistFromTarGz(data);

                        if (IsCached)
                        {
                            onProgress?.Invoke("编辑器资源就绪");
                            return;
                        }
                    }
                    catch
                    {
                        // 当前镜像失败，尝试下一个
                        continue;
                    }
                }

                onProgress?.Invoke("资源下载失败，使用在线CDN（可能较慢）");
            }
            finally
            {
                CacheLock.Release();
            }
        }

        /// <summary>
        /// 从 .tgz 文件中解压 dist/ 目录到缓存。
        /// npm 包结构: package/dist/... → 提取到 CacheDir/dist/...
        /// </summary>
        private static void ExtractDistFromTarGz(byte[] tgzData)
        {
            string tempDir = CacheDir + "_extracting";

            // 清理残留
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            try
            {
                // .tgz = gzip 压缩的 tar
                using (var ms = new MemoryStream(tgzData))
                using (var gz = new GZipStream(ms, CompressionMode.Decompress))
                {
                    TarFile.ExtractToDirectory(gz, tempDir, overwriteFiles: true);
                }

                // npm tarball 解压后在 package/ 子目录
                string srcDist = Path.Combine(tempDir, "package", "dist");
                if (!Directory.Exists(srcDist))
                    return;

                // 确保目标存在
                Directory.CreateDirectory(CacheDir);

                // 移动 dist/ 到缓存目录
                string destDist = Path.Combine(CacheDir, "dist");
                if (Directory.Exists(destDist))
                    Directory.Delete(destDist, true);

                Directory.Move(srcDist, destDist);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}

using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HyCADTool.Shared.Core
{
    /// <summary>
    /// 读 config.json 根对象。文件损坏时备份并拒绝当成空对象写回。
    /// </summary>
    public static class ConfigJsonRoot
    {
        public static JObject LoadOrCreate(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("path is required", nameof(filePath));

            if (!File.Exists(filePath))
                return new JObject();

            try
            {
                var parsed = JObject.Parse(File.ReadAllText(filePath));
                return parsed ?? new JObject();
            }
            catch (Exception ex)
            {
                var backup = CorruptFileBackup.Copy(filePath);
                throw new InvalidOperationException(
                    "config.json 解析失败，已备份到 " + backup + "，拒绝覆盖以免丢掉其它模块配置。",
                    ex);
            }
        }
    }

    /// <summary>把损坏的配置复制成 path.corrupt-时间戳，不改原文件。</summary>
    public static class CorruptFileBackup
    {
        public static string Copy(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("path is required", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("找不到要备份的文件。", filePath);

            var dest = filePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            File.Copy(filePath, dest, overwrite: false);
            return dest;
        }
    }
}

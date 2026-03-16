using Newtonsoft.Json;
using System;
using System.IO;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities
{
    internal static class CommandRelayStore
    {
        private static readonly string RelayFilePath =
            Path.Combine(Path.GetTempPath(), "HyCADTool.Refactored.command-relay.json");

        private sealed class RelayPayload
        {
            public string CommandKey { get; set; }
            public long Timestamp { get; set; }
        }

        public static void Save(string commandKey)
        {
            try
            {
                var payload = new RelayPayload
                {
                    CommandKey = commandKey,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                File.WriteAllText(RelayFilePath, JsonConvert.SerializeObject(payload));

                #region agent log
                AgentDebugLogger.Log("post-fix", "H12", "CommandRelayStore.Save", "saved relay command",
                    new { commandKey, relayFilePath = RelayFilePath });
                #endregion
            }
            catch
            {
            }
        }

        public static bool TryConsume(out string commandKey)
        {
            commandKey = null;

            try
            {
                if (!File.Exists(RelayFilePath))
                    return false;

                var json = File.ReadAllText(RelayFilePath);
                File.Delete(RelayFilePath);

                var payload = JsonConvert.DeserializeObject<RelayPayload>(json);
                commandKey = payload?.CommandKey;

                #region agent log
                AgentDebugLogger.Log("post-fix", "H12", "CommandRelayStore.TryConsume", "consumed relay command",
                    new { commandKey, relayFilePath = RelayFilePath });
                #endregion

                return !string.IsNullOrWhiteSpace(commandKey);
            }
            catch
            {
                return false;
            }
        }
    }
}

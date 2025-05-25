//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text.RegularExpressions;
//namespace CommandMethodScanner
//{
//    class Program
//    {
//        static void Main(string[] args)
//        {
//            string rootPath = @"E:\BaiduSyncdisk\Code\CSharp\Rebuild1framwork\HyCADtoolGpt\HyCADtool";
//            if (!Directory.Exists(rootPath))
//            {
//                Console.WriteLine($"目录不存在：{rootPath}");
//                return;
//            }
//            Console.WriteLine("正在扫描 CommandMethod 注册命令（忽略注释）...");
//            // 正则匹配 [CommandMethod("xxx")]
//            Regex regex = new Regex(@"\[CommandMethod\s*\(\s*""(?<name>[^""]+)""\s*\)\]", RegexOptions.Compiled);
//            Dictionary<string, List<string>> commandMap = new Dictionary<string, List<string>>();
//            foreach (var file in Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories))
//            {
//                string[] lines = File.ReadAllLines(file);
//                bool inBlockComment = false;
//                for (int i = 0; i < lines.Length; i++)
//                {
//                    string line = lines[i].Trim();
//                    // 跳过空行
//                    if (string.IsNullOrWhiteSpace(line)) continue;
//                    // 处理多行注释开始
//                    if (line.Contains("/*")) inBlockComment = true;
//                    if (inBlockComment)
//                    {
//                        if (line.Contains("*/"))
//                        {
//                            inBlockComment = false;
//                        }
//                        continue;
//                    }
//                    // 跳过单行注释
//                    int commentIndex = line.IndexOf("//");
//                    if (commentIndex >= 0)
//                    {
//                        line = line.Substring(0, commentIndex).Trim();
//                    }
//                    if (string.IsNullOrWhiteSpace(line)) continue;
//                    // 匹配命令
//                    Match match = regex.Match(line);
//                    if (match.Success)
//                    {
//                        string commandName = match.Groups["name"].Value;
//                        if (!commandMap.ContainsKey(commandName))
//                            commandMap[commandName] = new List<string>();
//                        commandMap[commandName].Add($"{file} (行 {i + 1})");
//                    }
//                }
//            }
//            Console.WriteLine("\n===== 重复命令名列表 =====");
//            var duplicates = commandMap.Where(kvp => kvp.Value.Count > 1);
//            if (!duplicates.Any())
//            {
//                Console.WriteLine("未发现重复的命令名！");
//            }
//            else
//            {
//                foreach (var dup in duplicates)
//                {
//                    Console.WriteLine($"\n命令名：{dup.Key}，出现 {dup.Value.Count} 次");
//                    foreach (var location in dup.Value)
//                    {
//                        Console.WriteLine("  -> " + location);
//                    }
//                }
//            }
//            Console.WriteLine("\n完成，按任意键退出...");
//            Console.ReadKey();
//        }
//    }
//}

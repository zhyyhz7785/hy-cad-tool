using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string rootPath = @"E:\BaiduSyncdisk\Code\CSharp\Rebuild1framwork\HyCADtoolGpt\EquipmentFoundation"; // 修改为你想要的路径

        // 定义目录结构
        var structure = new[]
        {
            "Core/EntityType.cs",
            "Core/DataConverter.cs",
            "Core/GeometryOperations.cs",
            "Core/ResultProcessor.cs",
            "Commands/CreateTypeCommand.cs",
            "Commands/ConvertCommand.cs",
            "Commands/OperationCommand.cs",
            "Commands/OutputCommand.cs",
            "Utils/AutoCADHelper.cs",
            "Utils/SelectionUtil.cs",
            "Utils/Logging.cs",
            "Models/CustomEntity.cs",
            "Models/ResultEntity.cs",           
            "App.config",
            "AutoCADPluginProject.csproj",
            "README.md"
        };

        try
        {
            // 创建根目录
            Directory.CreateDirectory(rootPath);
            Console.WriteLine($"Created root directory: {rootPath}");

            // 创建子目录和文件
            foreach (var item in structure)
            {
                string fullPath = Path.Combine(rootPath, item);
                string directory = Path.GetDirectoryName(fullPath);

                // 创建目录（如果不存在）
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Console.WriteLine($"Created directory: {directory}");
                }

                // 创建空文件
                File.Create(fullPath).Close();
                Console.WriteLine($"Created file: {fullPath}");
            }

            Console.WriteLine("Directory structure generated successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
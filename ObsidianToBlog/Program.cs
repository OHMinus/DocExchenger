using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ObsidianToBlog
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return;
            }

            var command = args[0];
            var commandArgs = args.Skip(1).ToArray();

            switch (command)
            {
                case "convert":
                    await RunConvert(commandArgs);
                    break;
                case "import":
                    await RunImport(commandArgs);
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    PrintUsage();
                    break;
            }
        }

        private static async Task RunConvert(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: convert <path> [--output <dir>]");
                return;
            }

            string path = args[0];
            string outputDir = "output";

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--output" && i + 1 < args.Length)
                {
                    outputDir = args[i + 1];
                    break;
                }
            }

            var converter = new ArticleConverter(outputDir);

            if (File.Exists(path))
            {
                converter.Convert(path);
            }
            else if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.md")
                                     .OrderBy(f => f)
                                     .ToArray();
                if (files.Length > 0)
                {
                    converter.ProcessDirectory(files);
                }
                else
                {
                    Console.WriteLine($"No markdown files found in '{path}'.");
                }
            }
            else
            {
                Console.WriteLine($"The path '{path}' does not exist.");
            }
        }

        private static async Task RunImport(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: import <platform> <file_path>");
                return;
            }

            var platform = args[0];
            var filePath = args[1];

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return;
            }

            switch (platform)
            {
                case "qiita":
                    var qiitaImporter = new QiitaImporter();
                    await qiitaImporter.Import(filePath);
                    break;
                default:
                    Console.WriteLine($"Importer for platform '{platform}' is not supported yet.");
                    break;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: dotnet run -- <command> [args]");
            Console.WriteLine("Commands:");
            Console.WriteLine("  convert <path> [--output <dir>]    Convert a file or directory.");
            Console.WriteLine("  import <platform> <file_path>      Import a file to a platform (e.g., qiita).");
        }
    }
}

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Markdig;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace ObsidianToPdf
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: ObsidianToPdf <input_directory> <output_pdf_path>");
                return;
            }

            string inputDir = Path.GetFullPath(args[0]);
            string outputPath = Path.GetFullPath(args[1]);

            if (!Directory.Exists(inputDir))
            {
                Console.WriteLine($"Error: Input directory '{inputDir}' does not exist.");
                return;
            }

            Console.WriteLine($"Input Vault: {inputDir}");
            Console.WriteLine($"Output PDF: {outputPath}");

            var converter = new ObsidianConverter(inputDir);
            await converter.ConvertAsync(outputPath);
            Console.WriteLine("Conversion Complete.");
        }
    }

    public class ObsidianConverter
    {
        private readonly string _root;
        private Dictionary<string, string> _fileMap;

        public ObsidianConverter(string root)
        {
            _root = root;
            _fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task ConvertAsync(string outputPath)
        {
            Console.WriteLine("Indexing files...");
            BuildFileMap();

            var mdFiles = Directory.GetFiles(_root, "*.md", SearchOption.AllDirectories);
            var htmlBuilder = new System.Text.StringBuilder();

            // Basic CSS for printing
            htmlBuilder.Append("<html><head><meta charset=\"UTF-8\" /><style>");
            htmlBuilder.Append("body { font-family: sans-serif; line-height: 1.6; padding: 20px; } ");
            htmlBuilder.Append("h1, h2, h3 { color: #333; } ");
            htmlBuilder.Append("img { max-width: 100%; height: auto; display: block; margin: 10px 0; } ");
            htmlBuilder.Append("blockquote { border-left: 4px solid #ccc; margin-left: 0; padding-left: 10px; color: #666; } ");
            htmlBuilder.Append("table { border-collapse: collapse; width: 100%; margin-bottom: 20px; } ");
            htmlBuilder.Append("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; } ");
            htmlBuilder.Append("th { background-color: #f2f2f2; } ");
            htmlBuilder.Append(".note-section { margin-bottom: 40px; } ");
            htmlBuilder.Append(".page-break { page-break-after: always; } ");
            htmlBuilder.Append("</style></head><body>");

            // Optional: Table of Contents
            htmlBuilder.Append("<h1>Table of Contents</h1><ul>");
            foreach (var mdFile in mdFiles)
            {
                 string name = Path.GetFileNameWithoutExtension(mdFile);
                 string slug = GetSlug(name);
                 htmlBuilder.Append($"<li><a href=\"#{slug}\">{name}</a></li>");
            }
            htmlBuilder.Append("</ul><div class=\"page-break\"></div>");

            Console.WriteLine($"Processing {mdFiles.Length} Markdown files...");

            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

            foreach (var mdFile in mdFiles)
            {
                string content = await File.ReadAllTextAsync(mdFile);
                string fileName = Path.GetFileNameWithoutExtension(mdFile);

                // Pre-process Obsidian Links
                string processedMarkdown = PreProcess(content);

                string html = Markdown.ToHtml(processedMarkdown, pipeline);
                string slug = GetSlug(fileName);

                htmlBuilder.AppendLine($"<div id=\"{slug}\" class=\"note-section\">");
                // Add a title for the note if it's not the first thing (optional, but good for context)
                // htmlBuilder.AppendLine($"<h1>{fileName}</h1>");
                // (Obsidian notes usually start with H1, so maybe skipping this to avoid double headers)

                htmlBuilder.AppendLine(html);
                htmlBuilder.AppendLine("</div>");
                htmlBuilder.AppendLine("<hr class='page-break' />");
            }

            htmlBuilder.Append("</body></html>");

            await GeneratePdfAsync(htmlBuilder.ToString(), outputPath);
        }

        private void BuildFileMap()
        {
            var files = Directory.GetFiles(_root, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                string fileNameNoExt = Path.GetFileNameWithoutExtension(file);

                // Map "Image.png" -> Full Path
                if (!_fileMap.ContainsKey(fileName))
                {
                    _fileMap[fileName] = file;
                }

                // Map "NoteName" -> Full Path (for .md files)
                if (!_fileMap.ContainsKey(fileNameNoExt))
                {
                    _fileMap[fileNameNoExt] = file;
                }
            }
        }

        private string GetSlug(string name)
        {
            // Allow Unicode (Japanese) but replace spaces and remove special chars
            string slug = name.Trim().Replace(" ", "-");
            // Remove characters that might be problematic in HTML IDs
            slug = Regex.Replace(slug, @"[\[\]\(\)\""\'\#\<\>\&]", "");
            return slug;
        }

        private string PreProcess(string markdown)
        {
            // 1. Handle Embeds ![[Target]]
            markdown = Regex.Replace(markdown, @"!\[\[(.*?)\]\]", match =>
            {
                string content = match.Groups[1].Value;
                // Handle resizing syntax: ![[image.png|100]] or ![[image.png|100x100]]
                string[] parts = content.Split('|');
                string target = parts[0];
                string size = parts.Length > 1 ? parts[1] : "";

                // Find file
                if (_fileMap.TryGetValue(target, out string fullPath))
                {
                    string src = new Uri(fullPath).AbsoluteUri;
                    string style = "";

                    if (!string.IsNullOrEmpty(size))
                    {
                        if (size.Contains('x'))
                        {
                            var dims = size.Split('x');
                            if (dims.Length == 2 && int.TryParse(dims[0], out int w) && int.TryParse(dims[1], out int h))
                            {
                                style = $"width=\"{w}\" height=\"{h}\"";
                            }
                        }
                        else if (int.TryParse(size, out int w))
                        {
                            style = $"width=\"{w}\"";
                        }
                    }

                    return $"<img src=\"{src}\" {style} />";
                }
                return match.Value; // Keep as is if not found
            });

            // 2. Handle WikiLinks [[Target]] or [[Target|Label]]
            markdown = Regex.Replace(markdown, @"\[\[(.*?)\]\]", match =>
            {
                string content = match.Groups[1].Value;
                string[] parts = content.Split('|');
                string target = parts[0];
                string label = parts.Length > 1 ? parts[1] : target;

                // Check if target note exists
                // We mapped "NoteName" -> path/to/NoteName.md
                if (_fileMap.TryGetValue(target, out string fullPath))
                {
                    // Link within the PDF
                    string slug = GetSlug(target);
                    return $"<a href=\"#{slug}\">{label}</a>";
                }

                // If not found, just return label text
                return label;
            });

            return markdown;
        }

        private async Task GeneratePdfAsync(string html, string outputPath)
        {
            Console.WriteLine("Checking browser...");
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            Console.WriteLine("Launching browser...");
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox" } // Often needed in container/sandbox environments
            });

            using var page = await browser.NewPageAsync();

            Console.WriteLine("Rendering PDF...");
            await page.SetContentAsync(html);

            await page.PdfAsync(outputPath, new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "20px",
                    Bottom = "20px",
                    Left = "20px",
                    Right = "20px"
                }
            });

            Console.WriteLine($"PDF saved to {outputPath}");
        }
    }
}

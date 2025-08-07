using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ObsidianToBlog
{
    public class ArticleConverter
    {
        private readonly string _baseOutputDir;

        public ArticleConverter(string baseOutputDir)
        {
            _baseOutputDir = baseOutputDir;
        }

        public void ProcessDirectory(string[] filePaths)
        {
            Console.WriteLine($"Processing directory with {filePaths.Length} files...");

            var zennSlugs = new List<string>();
            var articlesData = new List<(string title, string body, string zennSlug)>();

            // First pass: Read files and generate slugs
            foreach (var filePath in filePaths)
            {
                var fileContent = File.ReadAllText(filePath);
                TryParseFrontMatter(fileContent, out var frontMatter, out var body);
                var title = ExtractTitle(frontMatter) ?? Path.GetFileNameWithoutExtension(filePath);
                var zennSlug = GenerateZennSlug();

                zennSlugs.Add(zennSlug);
                articlesData.Add((title, body, zennSlug));
            }

            // Generate TOC
            var toc = GenerateToc(articlesData);
            Console.WriteLine("Generated Table of Contents:\n" + toc);

            // Second pass: Generate and write files
            foreach (var data in articlesData)
            {
                GenerateForZenn(data.title, data.body, data.zennSlug);
                GenerateForQiita(data.title, data.body, toc);
                GenerateForNote(data.title, data.body, toc);
            }

            // Generate book.yaml for Zenn
            GenerateZennBookFile(filePaths, zennSlugs);

            Console.WriteLine("Directory processing complete.");
        }

        public void Convert(string filePath, string toc = "")
        {
            Console.WriteLine($"Converting file: {filePath}");

            var fileContent = File.ReadAllText(filePath);
            TryParseFrontMatter(fileContent, out var frontMatter, out var body);
            var originalTitle = ExtractTitle(frontMatter) ?? Path.GetFileNameWithoutExtension(filePath);

            var zennSlug = GenerateZennSlug();
            GenerateForZenn(originalTitle, body, zennSlug);
            GenerateForQiita(originalTitle, body, toc);
            GenerateForNote(originalTitle, body, toc);

            Console.WriteLine($"Conversion complete for {Path.GetFileName(filePath)}.");
        }

        private string GenerateToc(List<(string title, string body, string zennSlug)> articles)
        {
            var tocBuilder = new StringBuilder();
            tocBuilder.AppendLine("## 目次");
            tocBuilder.AppendLine();

            for (int i = 0; i < articles.Count; i++)
            {
                var title = articles[i].title;
                var fileName = SanitizeTitleForFilename(title) + ".md";
                tocBuilder.AppendLine($"{i + 1}. [{title}]({fileName})");
            }
            return tocBuilder.ToString();
        }

        private void GenerateZennBookFile(string[] filePaths, List<string> zennSlugs)
        {
            if (filePaths.Length == 0) return;

            var directoryPath = Path.GetDirectoryName(filePaths[0]);
            var bookTitle = new DirectoryInfo(directoryPath).Name;

            var bookYamlContent = new StringBuilder();
            bookYamlContent.AppendLine($"title: \"{bookTitle}\"");
            bookYamlContent.AppendLine("summary: \"\"");
            bookYamlContent.AppendLine("topics: []");
            bookYamlContent.AppendLine("published: false");
            bookYamlContent.AppendLine("chapters:");
            foreach (var slug in zennSlugs)
            {
                bookYamlContent.AppendLine($"  - {slug}");
            }

            var zennArticlesDir = Path.Combine(_baseOutputDir, "zenn", "articles");
            var zennRepoRoot = Path.GetDirectoryName(zennArticlesDir);

            // This assumes the structure is /zenn_repo/articles, so book.yaml goes in /zenn_repo
            var bookFilePath = Path.Combine(zennRepoRoot, "book.yaml");

            Directory.CreateDirectory(zennRepoRoot);
            File.WriteAllText(bookFilePath, bookYamlContent.ToString());
            Console.WriteLine($"Generated Zenn book file at: {bookFilePath}");
        }


        private bool TryParseFrontMatter(string content, out string frontMatter, out string body)
        {
            frontMatter = string.Empty; body = content;
            if (!content.StartsWith("---")) return false;
            var secondMarkerIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
            if (secondMarkerIndex == -1) return false;
            frontMatter = content.Substring(3, secondMarkerIndex - 3).Trim();
            body = content.Substring(secondMarkerIndex + 3).Trim();
            return true;
        }

        private string ExtractTitle(string frontMatter)
        {
            if (string.IsNullOrWhiteSpace(frontMatter)) return null;
            try
            {
                var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(frontMatter);
                if (yamlObject.TryGetValue("title", out var title)) return title.ToString();
            }
            catch (Exception ex) { Console.WriteLine($"Error deserializing frontmatter: {ex.Message}"); }
            return null;
        }

        private string GenerateZennSlug()
        {
            // Path.GetRandomFileName() can be too short after removing the dot.
            // Using a GUID is a more reliable way to get a random string.
            return Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private void GenerateForZenn(string title, string body, string slug)
        {
            var newFrontMatter = $"---\ntitle: \"【{title}】\"\nemoji: 📝\ntype: tech\ntopics: []\npublished: false\n---";
            var newContent = $"{newFrontMatter}\n\n{body}";
            WriteToFile("zenn/articles", slug, newContent, ".md");
        }

        private void GenerateForQiita(string title, string body, string toc)
        {
            var newFrontMatter = $"---\ntitle: \"【{title}】\"\ntags: []\nprivate: true\n---";
            var contentWithToc = string.IsNullOrEmpty(toc) ? body : $"{toc}\n\n{body}";
            var newContent = $"{newFrontMatter}\n\n{contentWithToc}";
            WriteToFile("qiita", SanitizeTitleForFilename(title), newContent, ".md");
        }

        private void GenerateForNote(string title, string body, string toc)
        {
            var contentWithToc = string.IsNullOrEmpty(toc) ? body : $"{toc}\n\n{body}";
            var newContent = $"# 【{title}】\n\n{contentWithToc}";
            WriteToFile("note", SanitizeTitleForFilename(title), newContent, ".md");
        }

        private string SanitizeTitleForFilename(string title)
        {
            var sanitized = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
            return sanitized.Replace(' ', '_');
        }

        private void WriteToFile(string platformDir, string fileName, string content, string extension)
        {
            var outputDir = Path.Combine(_baseOutputDir, platformDir);
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, fileName + extension);
            File.WriteAllText(outputPath, content);
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ObsidianToBlog;
using System.IO;
using System.Linq;

namespace ObsidianToBlog.Tests
{
    [TestClass]
    public class ArticleConverterTests
    {
        private string _testInputDir;
        private string _testOutputDir;

        [TestInitialize]
        public void Setup()
        {
            // Create temporary directories for test input and output
            _testInputDir = Path.Combine(Path.GetTempPath(), "ObsidianToBlogTest_Input");
            _testOutputDir = Path.Combine(Path.GetTempPath(), "ObsidianToBlogTest_Output");
            Directory.CreateDirectory(_testInputDir);
            Directory.CreateDirectory(_testOutputDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up temporary directories
            if (Directory.Exists(_testInputDir)) Directory.Delete(_testInputDir, true);
            if (Directory.Exists(_testOutputDir)) Directory.Delete(_testOutputDir, true);
        }

        [TestMethod]
        public void ProcessDirectory_GeneratesCorrectBookAndToc()
        {
            // Arrange
            // Create dummy article files
            File.WriteAllText(Path.Combine(_testInputDir, "01-first.md"), "---\ntitle: First Post\n---\n\nContent 1");
            File.WriteAllText(Path.Combine(_testInputDir, "02-second.md"), "---\ntitle: Second Post\n---\n\nContent 2");

            var converter = new ArticleConverter(_testOutputDir);
            var files = Directory.GetFiles(_testInputDir, "*.md").OrderBy(f => f).ToArray();

            // Act
            converter.ProcessDirectory(files);

            // Assert
            // 1. Check for Zenn book.yaml
            var bookPath = Path.Combine(_testOutputDir, "zenn", "book.yaml");
            Assert.IsTrue(File.Exists(bookPath), "book.yaml should be created.");

            var bookContent = File.ReadAllText(bookPath);
            Assert.IsTrue(bookContent.Contains("title: \"ObsidianToBlogTest_Input\""), "Book title should be the directory name.");
            var chapterLines = bookContent.Split('\n').Count(l => l.Trim().StartsWith("-"));
            Assert.AreEqual(2, chapterLines, "Book should have 2 chapters.");

            // 2. Check for Qiita file with TOC
            var qiitaFilePath = Path.Combine(_testOutputDir, "qiita", "First_Post.md");
            Assert.IsTrue(File.Exists(qiitaFilePath), "Qiita file for the first post should be created.");

            var qiitaContent = File.ReadAllText(qiitaFilePath);
            Assert.IsTrue(qiitaContent.Contains("## 目次"), "Qiita file should contain a TOC heading.");
            Assert.IsTrue(qiitaContent.Contains("1. [First Post](First_Post.md)"), "TOC should contain the first post.");
            Assert.IsTrue(qiitaContent.Contains("2. [Second Post](Second_Post.md)"), "TOC should contain the second post.");
            Assert.IsTrue(qiitaContent.Contains("Content 1"), "Qiita file should contain the article content.");
        }
    }
}

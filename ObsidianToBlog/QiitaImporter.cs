using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ObsidianToBlog
{
    // Data model for Qiita API request
    public class QiitaPost
    {
        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("tags")]
        public List<QiitaTag> Tags { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }

    public class QiitaTag
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    // Data model for parsing frontmatter
    public class QiitaFrontmatter
    {
        public string Title { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public bool Private { get; set; } = true;
    }

    public class QiitaImporter
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public async Task Import(string filePath)
        {
            var apiToken = Environment.GetEnvironmentVariable("QIITA_API_TOKEN");
            if (string.IsNullOrEmpty(apiToken))
            {
                Console.WriteLine("Error: QIITA_API_TOKEN environment variable is not set.");
                return;
            }

            var fileContent = await File.ReadAllTextAsync(filePath);
            if (!TryParseFrontMatter(fileContent, out var frontMatterYaml, out var body))
            {
                Console.WriteLine("Error: Could not parse frontmatter from the file.");
                return;
            }

            var frontmatter = ParseQiitaFrontmatter(frontMatterYaml);
            if (frontmatter == null)
            {
                Console.WriteLine("Error: Could not deserialize frontmatter.");
                return;
            }

            var qiitaPost = new QiitaPost
            {
                Title = frontmatter.Title,
                Body = body,
                Private = frontmatter.Private,
                Tags = frontmatter.Tags.ConvertAll(t => new QiitaTag { Name = t })
            };

            await PostToQiita(qiitaPost, apiToken);
        }

        private QiitaFrontmatter ParseQiitaFrontmatter(string yaml)
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                return deserializer.Deserialize<QiitaFrontmatter>(yaml);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing Qiita frontmatter: {ex.Message}");
                return null;
            }
        }

        private async Task PostToQiita(QiitaPost post, string apiToken)
        {
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            Console.WriteLine($"Posting article '{post.Title}' to Qiita...");

            var response = await HttpClient.PostAsJsonAsync("https://qiita.com/api/v2/items", post);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Successfully posted to Qiita!");
                // Optionally, parse the response to get the URL of the new post
                // Console.WriteLine($"Response: {responseBody}");
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error posting to Qiita: {response.StatusCode}");
                Console.WriteLine($"Response: {errorBody}");
            }
        }

        // Duplicated from ArticleConverter. A shared utility class would be better.
        private bool TryParseFrontMatter(string content, out string frontMatter, out string body)
        {
            frontMatter = string.Empty;
            body = content;

            if (!content.StartsWith("---")) return false;

            var secondMarkerIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
            if (secondMarkerIndex == -1) return false;

            frontMatter = content.Substring(3, secondMarkerIndex - 3).Trim();
            body = content.Substring(secondMarkerIndex + 3).Trim();
            return true;
        }
    }
}

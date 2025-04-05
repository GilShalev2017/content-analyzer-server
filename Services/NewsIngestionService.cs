using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Confluent.Kafka;
using System.Text.Json; // Use System.Text.Json instead of Newtonsoft.Json
using System.Text.Json.Serialization;
using System.Linq;

namespace ContentModerationAndAnalyticsPlatform.Services
{
    public class NewsIngestionService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IProducer<Null, string> _kafkaProducer;

        private const string CONTENT_INGESTION_TOPIC = "your-kafka-topic";

        public NewsIngestionService(IHttpClientFactory httpClientFactory, IProducer<Null, string> kafkaProducer)
        {
            _httpClientFactory = httpClientFactory;
            _kafkaProducer = kafkaProducer;
        }

        // Types for NewsAPI response
        public class NewsAPIArticle
        {
            public Source source { get; set; }
            public string author { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public string url { get; set; }
            public string urlToImage { get; set; }
            public string publishedAt { get; set; }
            public string content { get; set; }
        }

        public class Source
        {
            public string id { get; set; }
            public string name { get; set; }
        }

        public class NewsAPIResponse
        {
            public string status { get; set; }
            public int totalResults { get; set; }
            public List<NewsAPIArticle> articles { get; set; }
        }

        // Types for the content we will produce to Kafka
        public class InsertContent
        {
            public string content_id { get; set; }
            public string content { get; set; }
            public string type { get; set; }
            public string user_id { get; set; }
            public Dictionary<string, object> metadata { get; set; }
        }

        public enum ContentTypes
        {
            NEWS
        }

        public async Task<bool> IngestNewsArticlesAsync()
        {
            try
            {
                var articles = await FetchNewsArticlesAsync();

                Console.WriteLine($"Fetched {articles.Count} news articles");

                foreach (var article in articles)
                {
                    if (string.IsNullOrEmpty(article.content) || string.IsNullOrEmpty(article.title))
                        continue;

                    var content = new InsertContent
                    {
                        content_id = Guid.NewGuid().ToString(), // Use UUID equivalent in C#
                        content = article.content,
                        type = ContentTypes.NEWS.ToString(),
                        user_id = "system",
                        metadata = new Dictionary<string, object>
                        {
                            { "title", article.title },
                            { "source", article.source.name },
                            { "author", article.author },
                            { "publishedAt", article.publishedAt },
                            { "url", article.url },
                            { "imageUrl", article.urlToImage }
                        }
                    };

                    await ProduceToKafkaAsync(content);
                    Console.WriteLine($"Ingested article: {article.title}");

                    // Add a small delay to avoid overwhelming the system
                    await Task.Delay(1000);
                }

                Console.WriteLine("News ingestion complete");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error ingesting news articles: {ex.Message}");
                return false;
            }
        }

        private async Task<List<NewsAPIArticle>> FetchNewsArticlesAsync()
        {
            using (var httpClient = _httpClientFactory.CreateClient())
            {
                // Fetch articles from the public NewsAPI
                var response = await httpClient.GetStringAsync("https://saurav.tech/NewsAPI/top-headlines/category/technology/us.json");

                // Deserialize the response
                var apiResponse = JsonSerializer.Deserialize<NewsAPIResponse>(response);

                if (apiResponse != null && apiResponse.status == "ok")
                {
                    return apiResponse.articles;
                }

                throw new Exception("Failed to fetch news articles");
            }
        }

        private async Task ProduceToKafkaAsync(InsertContent content)
        {
            // Serialize content to JSON using System.Text.Json
            var contentJson = JsonSerializer.Serialize(content);

            try
            {
                // Produce the content to Kafka topic
                await _kafkaProducer.ProduceAsync(CONTENT_INGESTION_TOPIC, new Message<Null, string> { Value = contentJson });
                Console.WriteLine($"Produced content to Kafka: {content.content_id}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error producing content to Kafka: {ex.Message}");
            }
        }
    }
}

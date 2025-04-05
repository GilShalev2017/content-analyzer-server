using Confluent.Kafka;
using ContentModerationAndAnalyticsPlatform.Models;
using Microsoft.Extensions.Configuration;
using Nest;

namespace ContentModerationAndAnalyticsPlatform.Services
{
    public class ElasticsearchService
    {
        private readonly ElasticClient _client;
        private readonly string _indexName;

        public ElasticsearchService(IConfiguration configuration)
        {
            var settings = new ConnectionSettings(new Uri(configuration["Elasticsearch:Uri"])).DefaultIndex(configuration["Elasticsearch:IndexName"]);

            _client = new ElasticClient(settings);
            
            _indexName = configuration["Elasticsearch:IndexName"];
        }
        
        public async Task IndexDocument(AnalysisResultModel document)
        {
            await _client.IndexDocumentAsync(document);
        }
        public async Task IndexAnalysisResult(AnalysisResultModel result)
        {
            await _client.IndexDocumentAsync(result);
        }

        public async Task<ISearchResponse<Models.Content>> SearchContent(string query)
        {
            var searchResponse = await _client.SearchAsync<Content>(s => s
                .Query(q => q.Match(m => m.Field(f => f.Text).Query(query))));

            return searchResponse;
        }

    }
}

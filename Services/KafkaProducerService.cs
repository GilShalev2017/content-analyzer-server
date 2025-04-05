using Confluent.Kafka;
using ContentModerationAndAnalyticsPlatform.Models;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ContentModerationAndAnalyticsPlatform.Services
{
    public class KafkaProducerService
    {
        private readonly IProducer<Null, string> _producer;
        private readonly string _contentTopic;
        private readonly string _analysisResultTopic;

        public KafkaProducerService(IConfiguration configuration)
        {
            var config = new ProducerConfig { BootstrapServers = configuration["Kafka:BootstrapServers"] };
            _producer = new ProducerBuilder<Null, string>(config).Build();
            _contentTopic = configuration["Kafka:ContentTopic"];
            _analysisResultTopic = configuration["Kafka:AnalysisResultTopic"];
        }

        public async Task ProduceContent(Models.Content content)
        {
            var message = new Message<Null, string> { Value = JsonSerializer.Serialize(content) };
            await _producer.ProduceAsync(_contentTopic, message);
        }

        public async Task ProduceAnalysisResult(AnalysisResultModel result)
        {
            var message = new Message<Null, string> { Value = JsonSerializer.Serialize(result) };
            await _producer.ProduceAsync(_analysisResultTopic, message);
        }
    }
}

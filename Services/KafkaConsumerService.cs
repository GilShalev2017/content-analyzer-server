using Confluent.Kafka;
using System.Text.Json;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ContentModerationAndAnalyticsPlatform.Models;
using System;

namespace ContentModerationAndAnalyticsPlatform.Services
{
    public class KafkaConsumerService : IHostedService
    {
        private readonly IConfiguration _configuration;
        private readonly ElasticsearchService _elasticsearchService;
        private WebSocket _webSocket;
        private CancellationTokenSource _stoppingCts;
        private Task _consumerTask;

        public KafkaConsumerService(IConfiguration configuration, ElasticsearchService elasticsearchService)
        {
            _configuration = configuration;
            _elasticsearchService = elasticsearchService;
        }

        public void SetWebSocket(WebSocket webSocket)
        {
            _webSocket = webSocket;

            // Start Kafka consumption when WebSocket is set
            if (_consumerTask == null || _consumerTask.IsCompleted)
            {
                _consumerTask = Task.Run(() => ConsumeKafkaMessagesAsync(_stoppingCts.Token));
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return Task.CompletedTask;
        }

        private async Task ConsumeKafkaMessagesAsync(CancellationToken token)
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _configuration["Kafka:BootstrapServers"],
                GroupId = "analysis-results-group",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
            var topicName = _configuration["Kafka:AnalysisResultTopic"];
            consumer.Subscribe(topicName);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(token);
                        var analysisResult = JsonSerializer.Deserialize<AnalysisResultModel>(consumeResult.Message.Value);

                        if (analysisResult != null)
                        {
                            await _elasticsearchService.IndexDocument(analysisResult);

                            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                            {
                                var json = JsonSerializer.Serialize(analysisResult);
                                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                await _webSocket.SendAsync(
                                    new ArraySegment<byte>(bytes),
                                    WebSocketMessageType.Text,
                                    true,
                                    token
                                );
                            }
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        Console.WriteLine($"Kafka error: {ex.Error.Reason}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Kafka consumer task was cancelled.");
            }
            finally
            {
                consumer.Close();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _stoppingCts?.Cancel();

            if (_consumerTask != null)
            {
                await _consumerTask;
            }
        }
    }
}

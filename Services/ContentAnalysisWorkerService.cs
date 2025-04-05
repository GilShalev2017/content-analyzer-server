using ContentModerationAndAnalyticsPlatform.Models;
using System.Text.Json;
using Confluent.Kafka;

namespace ContentModerationAndAnalyticsPlatform.Services
{
    public class ContentAnalysisWorkerService : IHostedService
    {
        private readonly IConfiguration _configuration;
        private readonly KafkaProducerService _producerService;
        private readonly OpenAIContentAnalyzer _contentAnalyzer;
        private CancellationTokenSource _stoppingCts;
        private Task _processingTask;

        public ContentAnalysisWorkerService(
            IConfiguration configuration,
            KafkaProducerService producerService,
            OpenAIContentAnalyzer contentAnalyzer)
        {
            _configuration = configuration;
            _producerService = producerService;
            _contentAnalyzer = contentAnalyzer;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _processingTask = Task.Run(() => ProcessContentAsync(_stoppingCts.Token));
            return Task.CompletedTask;
        }

        private async Task ProcessContentAsync(CancellationToken token)
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _configuration["Kafka:BootstrapServers"],
                GroupId = "content-processing-group",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
          
            consumer.Subscribe(_configuration["Kafka:ContentTopic"]);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(token);
                  
                        var content = JsonSerializer.Deserialize<Content>(consumeResult.Message.Value);

                        if (content != null)
                        {
                            var analysisResult = await _contentAnalyzer.AnalyzeContentAsync(content);

                            var result = new AnalysisResultModel
                            {
                                ContentId = content.ContentId,
                                Analysis = JsonSerializer.Serialize(analysisResult.AiData),
                                Score = analysisResult.Confidence,
                                Timestamp = DateTime.UtcNow,
                                Status = analysisResult.Status
                            };

                            await _producerService.ProduceAnalysisResult(result);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        Console.WriteLine($"Kafka error: {ex.Error.Reason}");
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Deserialization error: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Processing error: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Content analysis worker task was cancelled.");
            }
            finally
            {
                consumer.Close();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _stoppingCts?.Cancel();

            if (_processingTask != null)
            {
                await _processingTask;
            }
        }
    }
}

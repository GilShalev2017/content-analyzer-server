using ContentModerationAndAnalyticsPlatform.Models;
using ContentModerationAndAnalyticsPlatform.Services;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Security.Claims;


namespace ContentModerationAndAnalyticsPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class ContentController : ControllerBase
    {
        private readonly KafkaProducerService _kafkaProducer;
        private readonly ElasticsearchService _elasticsearchService;

        public ContentController(KafkaProducerService kafkaProducer, ElasticsearchService elasticsearchService)
        {
            _kafkaProducer = kafkaProducer;
            _elasticsearchService = elasticsearchService;
        }

        //[HttpPost]
        //public async Task<IActionResult> PostContent(ContentModel content)
        //{
        //    content.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    content.Timestamp = DateTime.UtcNow;
        //    content.Id = Guid.NewGuid().ToString();

        //    await _kafkaProducer.ProduceContent(content);
        //    return Ok();
        //}

        //[HttpPost]
        //public async Task<IActionResult> PostContent(dynamic input)
        //{
        //    string url = input.url;
        //    var web = new HtmlWeb();
        //    var doc = web.Load(url);
        //    string content = doc.DocumentNode.InnerText;

        //    // Use Open AI to analyze content
        //    string analysis = await _openAiService.Analyze(content);

        //    // Send results to Kafka
        //    AnalysisResultModel result = new AnalysisResultModel()
        //    {
        //        ContentId = Guid.NewGuid().ToString(),
        //        Analysis = analysis,
        //        Score = 1.0,
        //    };
        //    await _kafkaProducer.ProduceAnalysisResult(result);

        //    return Ok();
        //}

        [HttpGet("search")]
        public async Task<IActionResult> SearchContent(string query)
        {
            var results = await _elasticsearchService.SearchContent(query);
            return Ok(results.Documents);
        }

        // ContentController.cs
        [HttpPost("tech-companies")]
        public async Task<IActionResult> ExtractTechCompanies([FromBody] InputModel input)
        {
            if (input == null || string.IsNullOrEmpty(input.Url))
            {
                return BadRequest("URL is required.");
            }

            string url = input.Url;
            var web = new HtmlWeb();
            var doc = web.Load(url);
            string content = doc.DocumentNode.InnerText;

            // Extract Tech Company Names (Basic Example)

            List<string> techCompanies = ExtractCompanyNames(content);

            // Send results to Kafka
            AnalysisResultModel result = new AnalysisResultModel()
            {
                ContentId = Guid.NewGuid().ToString(),
                Analysis = String.Join(", ", techCompanies),
                Score = 1.0,
            };
            await _kafkaProducer.ProduceAnalysisResult(result);

            return Ok();
        }

        private List<string> ExtractCompanyNames(string text)
        {
            // This is a very basic example; you'll need a more robust approach.
            // For example, using an external API or a pre-built dictionary.

            List<string> companies = new List<string>();
            string[] knownCompanies = { "Google", "Apple", "Microsoft", "Amazon", "Tesla", "Meta", "Nvidia" }; //example of known companies.

            foreach (string company in knownCompanies)
            {
                if (text.Contains(company))
                {
                    companies.Add(company);
                }
            }
            return companies;
        }

        [HttpPost]
        public async Task<IActionResult> PostContent([FromBody] Content content)
        {
            content.Timestamp = DateTime.UtcNow;
            content.ContentId = Guid.NewGuid().ToString();

            await _kafkaProducer.ProduceContent(content);
            return Ok(new { content.ContentId });
        }

        //[HttpGet("/ws")]
        //public async Task Get()
        //{
        //    if (HttpContext.WebSockets.IsWebSocketRequest)
        //    {
        //        var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        //        _kafkaConsumerService.SetWebSocket(socket);

        //        while (socket.State == WebSocketState.Open)
        //        {
        //            await Task.Delay(1000); // Keep alive
        //        }
        //    }
        //    else
        //    {
        //        HttpContext.Response.StatusCode = 400;
        //    }
        //}

    }
}

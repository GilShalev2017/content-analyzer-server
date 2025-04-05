using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using ContentModerationAndAnalyticsPlatform.Models;



public class OpenAIContentAnalyzer
{
    private readonly string analysisAuthToken;
    private readonly string textAnalysisApiUrl = "https://api.openai.com/v1/chat/completions";
    private readonly string model = "gpt-4o";

    public OpenAIContentAnalyzer(string authToken)
    {
        analysisAuthToken = authToken;
    }

    public async Task<ContentAnalysisResult> AnalyzeContentAsync(Content content)
    {
        try
        {
            var textContent = GetTextContent(content);

            var systemMessage = $"You are a content moderation AI. Analyze the following content and determine if it violates any content policies. Specifically look for: hate speech, harassment, explicit content, or spam. Respond with a JSON object with the following fields: category, confidence, reasons, flagged.";

            var response = await CallOpenAIChatApi(systemMessage, textContent);

            var result = JsonSerializer.Deserialize<JsonElement>(response);
            var category = result.GetProperty("category").GetString();
            var confidence = result.GetProperty("confidence").GetDouble();
            var flagged = result.GetProperty("flagged").GetBoolean();

            var aiData = result;

            // Assuming some rules fetching logic (stubbed here)
            var rule = new Rule { Category = category, Active = true, Sensitivity = 75, AutoAction = "auto_remove" };

            var shouldFlag = (confidence >= rule.Sensitivity);
            var status = shouldFlag ? (rule.AutoAction == "auto_remove" ? "REMOVED" : "PENDING") : "APPROVED";

            return new ContentAnalysisResult
            {
                Category = category,
                Confidence = confidence,
                Flagged = shouldFlag,
                Status = status,
                AiData = aiData
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error analyzing content: {ex.Message}");
            return AnalyzeFallback(content);
        }
    }

    private async Task<string> CallOpenAIChatApi(string systemMessage, string content)
    {
        var httpClient = new HttpClient();
       
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", analysisAuthToken);

        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = systemMessage },
                new { role = "user", content = content }
            }
        };

        var contentJson = JsonSerializer.Serialize(requestBody);
    
        var stringContent = new StringContent(contentJson, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(textAnalysisApiUrl, stringContent);

        var jsonString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            return jsonString;
        }

        throw new Exception($"Error: {response.StatusCode} - {jsonString}");
    }

    private string GetTextContent(Content content)
    {
        if (content.Type == "news" && content.Metadata != null)
        {
            var metadata = content.Metadata as Dictionary<string, string>;
            return $"Title: {metadata?["title"] ?? "No title"}\nContent: {content.Text ?? "No content"}";
        }

        return $"Title: {content.Title ?? "No title"}\nContent: {content.Text ?? "No content"}";
    }

    private ContentAnalysisResult AnalyzeFallback(Content content)
    {
        var textContent = GetTextContent(content).ToLower();

        var hateKeywords = new List<string> { "hate", "racist", "discrimination", "bigot" };
        var harassmentKeywords = new List<string> { "harass", "bully", "threat", "stalking" };
        var explicitKeywords = new List<string> { "porn", "sex", "nude", "explicit" };
        var spamKeywords = new List<string> { "buy now", "click here", "free money", "discount", "limited time" };

        var category = "SAFE";
        var confidence = 0.0;
        var reasons = new List<string>();

        if (hateKeywords.Exists(keyword => textContent.Contains(keyword)))
        {
            category = "HATE_SPEECH";
            confidence = 75;
            reasons.Add("Contains keywords associated with hate speech");
        }
        else if (harassmentKeywords.Exists(keyword => textContent.Contains(keyword)))
        {
            category = "HARASSMENT";
            confidence = 70;
            reasons.Add("Contains keywords associated with harassment");
        }
        else if (explicitKeywords.Exists(keyword => textContent.Contains(keyword)))
        {
            category = "EXPLICIT";
            confidence = 85;
            reasons.Add("Contains keywords associated with explicit content");
        }
        else if (spamKeywords.Exists(keyword => textContent.Contains(keyword)))
        {
            category = "SPAM";
            confidence = 90;
            reasons.Add("Contains keywords associated with spam");
        }

        var rule = new Rule { Category = category, Active = true, Sensitivity = 75, AutoAction = "auto_remove" };
        var shouldFlag = (confidence >= rule.Sensitivity);
        var status = shouldFlag ? (rule.AutoAction == "auto_remove" ? "REMOVED" : "PENDING") : "APPROVED";

        return new ContentAnalysisResult
        {
            Category = category,
            Confidence = confidence,
            Flagged = shouldFlag,
            Status = status,
            AiData = new { category, confidence, reasons, flagged = shouldFlag }
        };
    }
}

//public class Content
//{
//    public string Type { get; set; }
//    public string ContentId { get; set; }
//    public string Title { get; set; }
//    public string Text { get; set; }
//    public string TextContent { get; set; }
//    public Dictionary<string, object> Metadata { get; set; }
//}

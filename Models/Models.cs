namespace ContentModerationAndAnalyticsPlatform.Models
{
    //public class ContentModel
    //{
    //    public string Id { get; set; }
    //    public string Text { get; set; }
    //    public string UserId { get; set; }
    //    public DateTime Timestamp { get; set; }
    //}

    public class InputModel
    {
        public string Url { get; set; }
    }

    public class Content
    {
        public string ContentId { get; set; }
        public string Type { get; set; }
        public string Text { get; set; }
        public string Title { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public string? UserId { get; set; }
        public DateTime? Timestamp { get; set; }
    }

    public class ContentAnalysisResult
    {
        public string Category { get; set; }
        public double Confidence { get; set; }
        public bool Flagged { get; set; }
        public string? Status { get; set; }
        public object? AiData { get; set; }
        public List<string>? Reasons { get; set; }
    }

    public class Rule
    {
        public string Category { get; set; }
        public bool Active { get; set; }
        public double Sensitivity { get; set; }
        public string AutoAction { get; set; }
    }

    public class AnalysisResultModel
    {
        public string ContentId { get; set; }
        public string Analysis { get; set; }
        public double Score { get; set; }
        public DateTime? Timestamp { get; set; }
        public string? Status { get; set; }
    }
}

namespace SmartShoppingChatBot.Application.Commons.Options
{
    public class GoogleConfigs
    {
        public string CredentialsPath { get; set; } = string.Empty;

        public string ProjectId { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string ModelId { get; set; } = string.Empty;

        public string EmbeddedModelId { get; set; } = string.Empty;

        public string EmbeddedLocation { get; set; } = string.Empty;

        public int OutputDimensionality { get; set; }

        public int GeminiMaxTokens { get; set; }
    }
}

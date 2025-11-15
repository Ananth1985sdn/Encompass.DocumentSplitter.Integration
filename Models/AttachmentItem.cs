using System.Text.Json.Serialization;

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class AttachmentItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("pages")]
        public List<AttachmentPage>? Pages { get; set; }

        [JsonPropertyName("originalUrls")]
        public List<string>? OriginalUrls { get; set; }
    }
}

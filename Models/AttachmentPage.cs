using System.Text.Json.Serialization;

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class AttachmentPage
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("thumbnail")]
        public ThumbnailItem? Thumbnail { get; set; }
    }
}

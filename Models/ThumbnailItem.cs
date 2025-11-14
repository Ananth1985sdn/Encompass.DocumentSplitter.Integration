using System.Text.Json.Serialization;

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class ThumbnailItem
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}

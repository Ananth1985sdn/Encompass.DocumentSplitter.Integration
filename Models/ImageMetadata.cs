using System.Text.Json.Serialization;

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class ImageMetadata
    {
        [JsonPropertyName("height")]
        public int Height { get; set; }
        [JsonPropertyName("width")]
        public int Width { get; set; }
        [JsonPropertyName("dpiX")]
        public int DpiX { get; set; }
        [JsonPropertyName("dpiY")]
        public int DpiY { get; set; }
        [JsonPropertyName("zipKey")]
        public required string ZipKey { get; set; }
        [JsonPropertyName("imageKey")]
        public required string ImageKey { get; set; }
    }
}

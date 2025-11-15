using System.Text.Json.Serialization;

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class Page
    {
        [JsonPropertyName("pageImage")]
        public ImageMetadata PageImage { get; set; }
        [JsonPropertyName("thumbnailImage")]
        public ImageMetadata ThumbnailImage { get; set; }

        public Page()
        {
            PageImage = new ImageMetadata
            {
                ZipKey = "",
                ImageKey = ""
            };

            ThumbnailImage = new ImageMetadata
            {
                ZipKey = "",
                ImageKey = ""
            };
        }
        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }
        [JsonPropertyName("rotation")]
        public int Rotation { get; set; }
        [JsonPropertyName("originalKey")]
        public string originalKey { get; set; } = string.Empty;
    }
}

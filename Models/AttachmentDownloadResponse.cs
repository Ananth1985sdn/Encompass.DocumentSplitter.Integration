using System.Text.Json.Serialization;

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class AttachmentDownloadResponse
    {
        [JsonPropertyName("attachments")]
        public List<AttachmentItem>? Attachments { get; set; }
    }
}

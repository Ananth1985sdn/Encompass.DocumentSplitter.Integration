using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json.Serialization;

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class DocumentAttachment
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("assignedTo")]
        public EntityInfo? AssignedTo { get; set; }

        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }

        [JsonPropertyName("isRemoved")]
        public bool IsRemoved { get; set; }

        [JsonPropertyName("createdBy")]
        public EntityInfo? CreatedBy { get; set; }

        [JsonPropertyName("createdDate")]
        public DateTime CreatedDate { get; set; }
        [JsonPropertyName("pages")]
        public List<Page> Pages { get; set; } = new();
    }
}

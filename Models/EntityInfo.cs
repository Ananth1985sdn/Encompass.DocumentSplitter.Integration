using System.Text.Json.Serialization;

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class EntityInfo
    {
        [JsonPropertyName("entityId")]
        public required string EntityId { get; set; }
        [JsonPropertyName("entityName")]
        public required string EntityName { get; set; }
        [JsonPropertyName("entityType")]
        public required string EntityType { get; set; }
        [JsonPropertyName("isActive")]
        public bool? IsActive { get; set; }
    }
}

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class UploadFileDetails
    {
        public FileMetadata File { get; set; } = new();
        public string Title { get; set; } = string.Empty;
    }
}

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class DocumentUploadRequest
    {
        public string LoanId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }
}

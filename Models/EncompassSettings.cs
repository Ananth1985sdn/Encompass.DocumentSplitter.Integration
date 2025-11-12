namespace Encompass.DocumentSplitter.Integration.Models
{
    public class EncompassSettings
    {
        public string EncompassApiBaseURL { get; set; } = string.Empty;
        public string EncompassUsername { get; set; } = string.Empty;
        public string EncompassPassword { get; set; } = string.Empty;
        public string EncompassClientId { get; set; } = string.Empty;
        public string EncompassClientSecret { get; set; } = string.Empty;
        public string EncompassScope { get; set; } = string.Empty;
        public string EncompassTokenURL { get; set; } = string.Empty;
        public string EncompassLoanPipelineURL { get; set; } = string.Empty;
        public string EncompassGetDocumentsURL { get; set; } = string.Empty;
        public string EncompassGetDocumentURL { get; set; } = string.Empty;
        public string DocumentPackageName { get; set; } = string.Empty;
    }
}

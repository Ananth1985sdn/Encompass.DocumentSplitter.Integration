namespace Encompass.DocumentSplitter.Integration.Interfaces
{
    public interface IEncompassDocumentUploadService
    {
        Task UploadDocumentsFromZipAsync(string zipFilePath, string loanId);
    }
}

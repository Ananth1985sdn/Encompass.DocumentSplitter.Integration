using Encompass.DocumentSplitter.Integration.Models;

namespace Encompass.DocumentSplitter.Integration.Interfaces
{
    public interface IEncompassService
    {
        string HealthCheck();
        Task<string> GetEncompassTokenAsync();
        Task UploadToEfolderAsync(DocumentUploadRequest request);
        Task<byte[]?> GetLoanFileAsync(string loanId);
    }
}

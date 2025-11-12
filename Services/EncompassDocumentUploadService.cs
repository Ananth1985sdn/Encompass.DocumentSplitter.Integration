using Encompass.DocumentSplitter.Integration.Interfaces;
using Encompass.DocumentSplitter.Integration.Models;
using System.IO.Compression;

namespace Encompass.DocumentSplitter.Integration.Services
{
    public class EncompassDocumentUploadService : IEncompassDocumentUploadService
    {
        private readonly IEncompassService _encompassService; 
        private readonly ILogger<EncompassDocumentUploadService> _logger;
        public EncompassDocumentUploadService(
        IEncompassService encompassService,
        ILogger<EncompassDocumentUploadService> logger)
        {
            _encompassService = encompassService;
            _logger = logger;
        }

        public async Task UploadDocumentsFromZipAsync(string zipFilePath, string loanId)
        {
            // 1️⃣ Extract ZIP to temp directory
            string extractPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(zipFilePath));
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);
            ZipFile.ExtractToDirectory(zipFilePath, extractPath);

            _logger.LogInformation("Extracted ZIP to {ExtractPath}", extractPath);

            // 2️⃣ Enumerate all folders and PDFs
            foreach (var folder in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
            {
                string categoryName = new DirectoryInfo(folder).Name;

                foreach (var pdfFile in Directory.GetFiles(folder, "*.pdf", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        _logger.LogInformation("Uploading {File} under category {Category}", pdfFile, categoryName);

                        var uploadRequest = new DocumentUploadRequest
                        {
                            LoanId = loanId,
                            CategoryName = categoryName,
                            FilePath = pdfFile
                        };

                        await _encompassService.UploadToEfolderAsync(uploadRequest);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading file {File}", pdfFile);
                    }
                }
            }

            Directory.Delete(extractPath, true);
            _logger.LogInformation("Cleaned up extracted folder {ExtractPath}", extractPath);
        }
    }
}

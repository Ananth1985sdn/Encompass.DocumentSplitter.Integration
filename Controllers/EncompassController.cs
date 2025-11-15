using Encompass.DocumentSplitter.Integration.Interfaces;
using Encompass.DocumentSplitter.Integration.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Encompass.DocumentSplitter.Integration.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EncompassController : ControllerBase
    {
        private readonly IEncompassService _encompassService;
        private readonly IEncompassDocumentUploadService _documentUploadService;
        public EncompassController(IEncompassService encompassService, IEncompassDocumentUploadService documentUploadService)
        {
            _encompassService = encompassService;
            _documentUploadService = documentUploadService;
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            var message = _encompassService.HealthCheck();
            return Ok(message);
        }

        [HttpGet("GetEncompassToken")]
        public async Task<IActionResult> GetEncompassToken()
        {
            var token = await _encompassService.GetEncompassTokenAsync();
            return Ok(new { token });
        }
        [HttpPost("upload-zip")]
        public async Task<IActionResult> UploadZip([FromForm] UploadZipRequest request)
        {
            var zipFile = request.ZipFile;
            var loanId = request.LoanId;

            if (zipFile == null || zipFile.Length == 0)
                return BadRequest("Invalid ZIP file.");

            // Save ZIP file temporarily
            string tempZipPath = Path.Combine(Path.GetTempPath(), zipFile.FileName);
            using (var stream = new FileStream(tempZipPath, FileMode.Create))
            {
                await zipFile.CopyToAsync(stream);
            }

            // Process ZIP file (e.g., extract and upload to eFolder)
            await _documentUploadService.UploadDocumentsFromZipAsync(tempZipPath, loanId);

            // Clean up
            System.IO.File.Delete(tempZipPath);

            return Ok("Documents uploaded successfully to eFolder.");
        }
        [HttpGet("GetLoanFile/{loanId}")]
        public async Task<IActionResult> GetLoanFile(string loanId)
        {
            if (string.IsNullOrWhiteSpace(loanId))
                return BadRequest("LoanId is required.");

            var loan = await _encompassService.GetLoanFileAsync(loanId);

            if (loan == null)
                return NotFound($"Loan not found: {loanId}");

            return Ok(loan);
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Encompass.DocumentSplitter.Integration.Models
{
    public class UploadZipRequest
    {
        [Required]
        public IFormFile ZipFile { get; set; }

        [Required]
        public string LoanId { get; set; }
    }
}

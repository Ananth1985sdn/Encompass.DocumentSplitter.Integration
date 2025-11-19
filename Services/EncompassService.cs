using Encompass.DocumentSplitter.Integration.Interfaces;
using Encompass.DocumentSplitter.Integration.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
//using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.IO.Compression;

namespace Encompass.DocumentSplitter.Integration.Services
{
    public class EncompassService : IEncompassService
    {
        private readonly EncompassSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private const string TokenCacheKey = "EncompassAccessToken";
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<EncompassService> _logger;
        public EncompassService(IOptions<EncompassSettings> options, IMemoryCache cache, IWebHostEnvironment env, ILogger<EncompassService> logger, HttpClient? httpClient = null)
        {
            _settings = options.Value;
            _cache = cache;
            _env = env;
            _httpClient = httpClient ?? new HttpClient();
            _logger = logger;
        }
        public string HealthCheck()
        {
            return "Encompass Document Splitter Integration is running.";
        }
        public async Task<string> GetEncompassTokenAsync()
        {
            if (_cache.TryGetValue(TokenCacheKey, out string accessToken))
            {
                return accessToken;
            }

            var fullUrl = $"{_settings.EncompassApiBaseURL}{_settings.EncompassTokenURL}";

            var requestBody = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("username", _settings.EncompassUsername),
            new KeyValuePair<string, string>("password", _settings.EncompassPassword),
            new KeyValuePair<string, string>("client_id", _settings.EncompassClientId),
            new KeyValuePair<string, string>("client_secret", _settings.EncompassClientSecret),
            new KeyValuePair<string, string>("scope", _settings.EncompassScope)
            });

            var response = await _httpClient.PostAsync(fullUrl, requestBody);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });


            if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
                throw new InvalidOperationException("Access token missing in response.");

            accessToken = tokenResponse.AccessToken;
            int expiresIn = tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 1800;
            var cacheDuration = TimeSpan.FromSeconds(Math.Max(60, expiresIn - 60));
            _cache.Set(TokenCacheKey, accessToken, cacheDuration);

            return accessToken;
        }
        public async Task UploadToEfolderAsync(DocumentUploadRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!_cache.TryGetValue(TokenCacheKey, out string token))
            {
                token = await GetEncompassTokenAsync();
                _cache.Set(TokenCacheKey, token, TimeSpan.FromMinutes(55));
            }
           
            string getDocsUrl =
                $"{_settings.EncompassApiBaseURL}/encompass/v3/loans/{request.LoanId}/documents";

            using var getDocsReq = new HttpRequestMessage(HttpMethod.Get, getDocsUrl);
            getDocsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var docsResponse = await _httpClient.SendAsync(getDocsReq);
            docsResponse.EnsureSuccessStatusCode();

            var docsJson = await docsResponse.Content.ReadAsStringAsync();
            using var docs = JsonDocument.Parse(docsJson);

            string? existingDocumentId = null;

            foreach (var doc in docs.RootElement.EnumerateArray())
            {
                string title = doc.GetProperty("title").GetString() ?? string.Empty;

                if (title.Equals(request.CategoryName, StringComparison.OrdinalIgnoreCase))
                {
                    existingDocumentId = doc.GetProperty("id").GetString();
                    break;
                }
            }

            string documentEntityId = existingDocumentId ?? string.Empty;

            if (existingDocumentId == null)
            {

                var documentPayload = new[]
                {
                new
                {
                    title = request.CategoryName,
                    description = $"{request.CategoryName} document created by integration"
                }
            };

                var createDocUrl =
                    $"{_settings.EncompassApiBaseURL}/encompass/v3/loans/{request.LoanId}/documents?action=add&view=entity";

                using var createRequest = new HttpRequestMessage(HttpMethod.Patch, createDocUrl);
                createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                createRequest.Content = new StringContent(JsonSerializer.Serialize(documentPayload), Encoding.UTF8, "application/json");

                var createResponse = await _httpClient.SendAsync(createRequest);
                createResponse.EnsureSuccessStatusCode();

                var createdDocJson = await createResponse.Content.ReadAsStringAsync();
                using var docJson = JsonDocument.Parse(createdDocJson);
                documentEntityId = docJson.RootElement[0].GetProperty("id").GetString();
            }

           
            var uploadMeta = new
            {
                file = new
                {
                    contentType = "application/pdf",
                    name = Path.GetFileName(request.FilePath),
                    size = new FileInfo(request.FilePath).Length
                },
                title = Path.GetFileName(request.FilePath),
                assignTo = new
                {
                    entityId = documentEntityId,
                    entityType = "Document"
                }
            };

            var uploadUrlReq = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_settings.EncompassApiBaseURL}/encompass/v3/loans/{request.LoanId}/attachmentUploadUrl"
            );

            uploadUrlReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadUrlReq.Content = new StringContent(JsonSerializer.Serialize(uploadMeta), Encoding.UTF8, "application/json");

            var uploadUrlResponse = await _httpClient.SendAsync(uploadUrlReq);
            uploadUrlResponse.EnsureSuccessStatusCode();

            var uploadUrlJson = await uploadUrlResponse.Content.ReadAsStringAsync();
            using var uploadDoc = JsonDocument.Parse(uploadUrlJson);
            var uploadUrl = uploadDoc.RootElement.GetProperty("uploadUrl").GetString();
            var authHeader = uploadDoc.RootElement.GetProperty("authorizationHeader").GetString();
        

            using var fileStream = File.OpenRead(request.FilePath);
            using var uploadClient = new HttpClient();
            using var uploadReq = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
            {
                Content = new StreamContent(fileStream)
            };

            uploadReq.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            uploadReq.Headers.Add("Authorization", authHeader);

            var uploadResponse = await uploadClient.SendAsync(uploadReq);
            uploadResponse.EnsureSuccessStatusCode();

            Console.WriteLine($"✅ Successfully uploaded to eFolder → DocumentId: {documentEntityId}");
        }
        public async Task<string> GetLoanFileAsync(string loanId)
        {
            //string requestUrl = "https://eopp9b3n3fow3qp.m.pipedream.net";
            //string requestUrl = "http://13.83.50.15:5002/save_pdf";
            string requestUrl = "http://10.10.0.4:5002/save_pdf";
            //byte[] pythonServiceResponseContent = string.Empty;
            if (!_cache.TryGetValue(TokenCacheKey, out string token))
            {
                token = await GetEncompassTokenAsync();
                _cache.Set(TokenCacheKey, token, TimeSpan.FromMinutes(55));
            }

            string? pdfUrl = await DownloadLoanAttachmentsAsync(loanId, "Source");

            if (string.IsNullOrWhiteSpace(pdfUrl))
            {
                return null;
            }

            byte[] fileBytes;

            try
            {
                fileBytes = await _httpClient.GetByteArrayAsync(pdfUrl);
            }
            catch (Exception ex)
            {
                return null;
            }

            try
            {

                string targetDirectory = Path.Combine(_env.ContentRootPath, "LoanData");

                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                string dateString = DateTime.Now.Date.ToString("yyyy-MM-dd");

                string filePath = Path.Combine(targetDirectory, $"{loanId}_{dateString}.pdf");
                string fullPath = Path.Combine(targetDirectory, filePath);
                await File.WriteAllBytesAsync(filePath, fileBytes);

                try
                {
                    using var fileStream = File.OpenRead(filePath);
                    using var multipartContent = CreateMultipartContent(fileStream, $"{loanId}_{dateString}.pdf");
                    var pythonServiceResponse = await _httpClient.PostAsync(requestUrl, multipartContent);
                    pythonServiceResponse.EnsureSuccessStatusCode();
                    var zipBytes = await pythonServiceResponse.Content.ReadAsByteArrayAsync();

                    var contentType = pythonServiceResponse.Content.Headers.ContentType?.MediaType;

                    if (contentType != null && !contentType.Contains("zip", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Invalid Response: Expected ZIP but got Content-Type: {contentType}";
                    }

                    bool isZip = zipBytes.Length > 4 &&
                                 zipBytes[0] == 0x50 &&
                                 zipBytes[1] == 0x4B &&
                                 zipBytes[2] == 0x03 &&
                                 zipBytes[3] == 0x04;

                    if (!isZip)
                    {
                        return "Invalid Response: ZIP signature not found. Response is not a ZIP file.";
                    }

                    if (zipBytes.Length < 22)
                    {
                        return "Invalid Response: ZIP file too small.";
                    }

                    var zipPath = Path.Combine(targetDirectory, $"{loanId}_{dateString}.zip");
                    string fullZipPath = Path.Combine(targetDirectory, zipPath);
                    await File.WriteAllBytesAsync(zipPath, zipBytes);

                    try
                    {
                        await UploadDocumentsFromZipAsync(zipPath, loanId);

                        File.Delete(zipPath);
                    }
                    catch (Exception ex)
                    {
                        return $" Issue in Extract Zip File: {ex.Message}";
                    }


                }
                catch (Exception ex)
                {
                    return $"Upload to Document Splitter Service Failed: {ex.Message}";
                }

            }
            catch (Exception ex)
            {
                return $"Loan File PDF Creation Failed {ex.Message}";
            }

            return "Upload and processing completed successfully.";
        }
        public async Task<string> DownloadLoanAttachmentsAsync(string loanId, string sourceEntityName)
        {
            string token;
            if (!_cache.TryGetValue(TokenCacheKey, out token))
            {
                token = await GetEncompassTokenAsync();
                _cache.Set(TokenCacheKey, token, TimeSpan.FromMinutes(55));
            }

            var attachments = await GetAttachmentsAsync(loanId, token);

            if (attachments == null || attachments.Count == 0)
                return string.Empty;

            var filtered = attachments
                .Where(a => a.AssignedTo?.EntityName?.Equals(sourceEntityName, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (filtered.Count == 0)
                return string.Empty;

            var attachmentIds = filtered.Select(a => a.Id).ToList();

            var downloadUrls = await GetAttachmentDownloadUrlsAsync(loanId, token, attachmentIds);
            return downloadUrls;

        }
        public async Task<List<DocumentAttachment>> GetAttachmentsAsync(string loanId, string token)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_settings.EncompassApiBaseURL}/encompass/v3/loans/{loanId}/attachments?includeRemoved=true");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<DocumentAttachment>>(json);
        }
        public async Task<string> GetAttachmentDownloadUrlsAsync(string loanId, string token, List<string> attachmentIds)
        {
            string downloadUrls = string.Empty;

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_settings.EncompassApiBaseURL}/encompass/v3/loans/{loanId}/attachmentDownloadUrl");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Accept", "application/json");

            var body = new
            {
                attachments = attachmentIds
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<AttachmentDownloadResponse>(json);

            if (result?.Attachments == null || result.Attachments.Count == 0)
                return downloadUrls;

            foreach (var attachment in result.Attachments)
            {
                if (attachment.OriginalUrls != null && attachment.OriginalUrls.Count > 0)
                {
                    return attachment.OriginalUrls[0];
                }
            }

            return downloadUrls;
        }
        public static MultipartFormDataContent CreateMultipartContent(FileStream fileStream, string fileName)
        {
            //var content = new MultipartFormDataContent();
            //string fileKey = "12345";
            //var fileContent = new StreamContent(fileStream);
            //fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

            //content.Add(fileContent, "pdf_file", fileName);

            //content.Add(new StringContent(fileKey), "file_key");

            //return content;

            var content = new MultipartFormDataContent();

            // PDF file
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "pdf_file", fileName);

            // Other form fields (matching curl)
            content.Add(new StringContent("12345"), "file_key");
            content.Add(new StringContent("LoanData"), "input_path");
            content.Add(new StringContent("LoanData"), "output_path");
            content.Add(new StringContent("true"), "flag");

            return content;
        }
        private async Task UploadDocumentsFromZipAsync(string zipFilePath, string loanId)
        {
            string extractPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(zipFilePath));
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);
            ZipFile.ExtractToDirectory(zipFilePath, extractPath);

            _logger.LogInformation("Extracted ZIP to {ExtractPath}", extractPath);

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

                        await UploadToEfolderAsync(uploadRequest);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading file {File} - {loanId}", pdfFile, loanId);
                    }
                }
            }

            Directory.Delete(extractPath, true);
            _logger.LogInformation("Cleaned up extracted folder {ExtractPath}", extractPath);
        }

    }
}

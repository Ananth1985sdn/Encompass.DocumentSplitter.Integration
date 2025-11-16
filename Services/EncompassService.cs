using Encompass.DocumentSplitter.Integration.Interfaces;
using Encompass.DocumentSplitter.Integration.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
//using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Encompass.DocumentSplitter.Integration.Services
{
    public class EncompassService : IEncompassService
    {
        private readonly EncompassSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private const string TokenCacheKey = "EncompassAccessToken";
        private readonly IWebHostEnvironment _env;
        public EncompassService(IOptions<EncompassSettings> options, IMemoryCache cache, IWebHostEnvironment env,HttpClient? httpClient = null)
        {
            _settings = options.Value;
            _cache = cache;
            _env = env;
            _httpClient = httpClient ?? new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
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

            string token;
            if (!_cache.TryGetValue(TokenCacheKey, out token))
            {
                token = await GetEncompassTokenAsync();
                _cache.Set(TokenCacheKey, token, TimeSpan.FromMinutes(55));
            }

            var documentPayload = new[]
            {
                new
                {
                    title = request.CategoryName,
                    description = $"{request.CategoryName} document created by integration"
                }
            };

            var createDocUrl = $"{_settings.EncompassApiBaseURL}/encompass/v3/loans/{request.LoanId}/documents?action=add&view=entity";

            using var createRequest = new HttpRequestMessage(HttpMethod.Patch, createDocUrl);
            createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createRequest.Content = new StringContent(JsonSerializer.Serialize(documentPayload), Encoding.UTF8, "application/json");

            var createResponse = await _httpClient.SendAsync(createRequest);
            if (!createResponse.IsSuccessStatusCode)
            {
                var errorBody = await createResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create eFolder document: {createResponse.StatusCode}, {errorBody}");
            }

            var createdDocJson = await createResponse.Content.ReadAsStringAsync();
            using var docJson = JsonDocument.Parse(createdDocJson);
            var documentEntityId = docJson.RootElement[0].GetProperty("id").GetString();

            var uploadMeta = new
            {
                file = new
                {
                    contentType = "application/pdf",
                    name = Path.GetFileName(request.FilePath),
                    size = new FileInfo(request.FilePath).Length
                },
                title = request.CategoryName,
                assignTo = new
                {
                    entityId = documentEntityId,
                    entityType = "Document"
                }
            };

            var uploadUrlRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_settings.EncompassApiBaseURL}/encompass/v3/loans/{request.LoanId}/attachmentUploadUrl"
            );
            uploadUrlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            uploadUrlRequest.Content = new StringContent(JsonSerializer.Serialize(uploadMeta), Encoding.UTF8, "application/json");

            var uploadUrlResponse = await _httpClient.SendAsync(uploadUrlRequest);
            if (!uploadUrlResponse.IsSuccessStatusCode)
            {
                var errorBody = await uploadUrlResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get upload URL: {uploadUrlResponse.StatusCode}, {errorBody}");
            }

            var uploadUrlJson = await uploadUrlResponse.Content.ReadAsStringAsync();
            using var uploadDoc = JsonDocument.Parse(uploadUrlJson);
            var uploadUrl = uploadDoc.RootElement.GetProperty("uploadUrl").GetString();
            var authHeader = uploadDoc.RootElement.GetProperty("authorizationHeader").GetString();

            using var fileStream = File.OpenRead(request.FilePath);
            using var uploadClient = new HttpClient();
            using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
            {
                Content = new StreamContent(fileStream)
            };

            uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

            uploadRequest.Headers.Add(
                "Authorization",
                authHeader
            );

            var uploadResponse = await uploadClient.SendAsync(uploadRequest);
            if (!uploadResponse.IsSuccessStatusCode)
            {
                var errorBody = await uploadResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to upload file: {uploadResponse.StatusCode}, {errorBody}");
            }

            Console.WriteLine($"✅ Successfully uploaded {request.FilePath} to Encompass eFolder.");
        }
        public async Task<string> GetLoanFileAsync(string loanId)
        {
            //string requestUrl = "https://eopp9b3n3fow3qp.m.pipedream.net";
            string requestUrl = "http://13.83.50.15:5002/save_pdf";
            string pythonServiceResponseContent = string.Empty;
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
                    pythonServiceResponseContent = await pythonServiceResponse.Content.ReadAsStringAsync();
                }
                catch(Exception ex)
                {
                    return $"Upload to Document Splitter Service Failed: {ex.Message}";
                }

            }
            catch (Exception ex)
            {
                return $"Loan File PDF Creation Failed {ex.Message}";
            }

            return pythonServiceResponseContent;
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

        public async Task<string> GetAttachmentDownloadUrlsAsync(string loanId,string token,List<string> attachmentIds)
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

        //public static MultipartFormDataContent CreateMultipartContent(FileStream fileStream, string fileName)
        //{
        //    var content = new MultipartFormDataContent();
        //    content.Add(new StreamContent(fileStream), "file", fileName);
        //    return content;
        //}

        public static MultipartFormDataContent CreateMultipartContent(FileStream fileStream, string fileName)
        {
            //var content = new MultipartFormDataContent();
            //string fileKey = "12345";
            //// Add PDF file
            //content.Add(new StreamContent(fileStream), "pdf_file", fileName);

            //// Add file_key as string content
            //content.Add(new StringContent(fileKey), "file_key");

            //return content;

            var content = new MultipartFormDataContent();
            string fileKey = "12345";
            // Set the content type explicitly to application/pdf
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

            // Add PDF file
            content.Add(fileContent, "pdf_file", fileName);

            // Add file_key as string content
            content.Add(new StringContent(fileKey), "file_key");

            return content;
        }

    }
}

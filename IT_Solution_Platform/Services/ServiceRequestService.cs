using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using IT_Solution_Platform.Models;
using Newtonsoft.Json;
using Supabase;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace IT_Solution_Platform.Services
{
    /// <summary>
    /// Implementation for managing service requests.
    /// </summary>
    public class ServiceRequestService
    {
        // Consider injecting Supabase Client via DI if possible in your setup
        private readonly AuditLogService _auditLogService;
        private readonly SupabaseDatabase _databaseService;
        private readonly Supabase.Client _supabaseClient;

        // Remove the instance client - use ClientManager instead
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public ServiceRequestService()
        { 
            _databaseService = new SupabaseDatabase();
            _auditLogService = new AuditLogService(_databaseService);
            _supabaseClient = SupabaseConfig.GetServiceClient();
        }

        // Define the bucket name where documents will be stored in Supabase Storage
        private const string ServiceRequestDocumentBucket = "service-request-documents"; // CHANGE BUCKET NAME AS NEEDED
        private const string ServiceRequestType = "ServiceRequest";

        public async Task<int?> CreatePenTestingServiceRequestAsync(PenTestingRequestViewModel model, int userId, string accessToken = null)
        {
            var refreshToken = HttpContext.Current?.Request.Cookies["refresh_token"]?.Value;
            if (model == null) 
            {
                _auditLogService.LogAudit
                (
                    userId,
                    ServiceRequestType,
                    ErrorType.InvalidInput.ToString(),
                    null,
                    new { ErrorMessage = "Model or Supabase Client is null" },
                    IpAddress,
                    UserAgent
                );
                return null;
            }
            try
            {
                // 1. Prepare request detail
                var requestDetailsDict = new Dictionary<string, object>
                {
                     { "TargetSystem", model.TargetSystem },
                     { "ScopeDescription", model.ScopeDescription },
                     { "TestingObjectives", model.TestingObjectives },
                     { "PreferredStartDate", model.PreferredStartDate?.ToString("yyyy-MM-dd") },
                     { "PreferredEndDate", model.PreferredEndDate?.ToString("yyyy-MM-dd") },
                     { "PrimaryContactName", model.PrimaryContactName },
                     { "PrimaryContactEmail", model.PrimaryContactEmail },
                     { "PrimaryContactPhone", model.PrimaryContactPhone },
                     { "ComplianceRequirements", model.ComplianceRequirements },
                     { "AdditionalNotes", model.AdditionalNotes }
                };

                string requestDetailsJson = JsonConvert.SerializeObject(requestDetailsDict);

                // 2. Create ServiceRequest Object
                var newRequest = new ServiceRequest
                {
                    UserId = userId,
                    ServiceId = model.ServiceId,
                    StatusId = 1, // Default 'submitted' status
                    RequestDetails = requestDetailsJson,
                    RequestedDate = DateTime.UtcNow,
                    Notes = "Submitted via web form",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // 3. Handle file uploads first (if any)
                var uploadedDocuments = new List<ServiceRequestDocument>();
                if (model.SupportingDocuments != null && model.SupportingDocuments.Any())
                {
                    // Get the current user ID for folder structure
                    string userFolder;
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        var currentUser = _supabaseClient.Auth.CurrentUser;
                        userFolder = currentUser?.Id ?? $"user_{userId}";
                    }
                    else
                    {
                        userFolder = $"user_{userId}";
                    }

                    foreach (var file in model.SupportingDocuments)
                    {
                        if (file == null || file.ContentLength == 0)
                        {
                            _auditLogService.LogAudit(
                                userId,
                                ServiceRequestType,
                                ErrorType.InvalidFile.ToString(),
                                null,
                                new { ErrorMessage = "Null or empty file provided", Filename = file?.FileName },
                                IpAddress,
                                UserAgent
                            );
                            return null; // Fail early if any file is invalid
                        }

                        var allowedExtenstion = new[] { ".pdf", ".doc", ".docs", ".txt" };
                        var extenstion = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!allowedExtenstion.Contains(extenstion))
                        {
                            _auditLogService.LogAudit(
                                userId,
                                ServiceRequestType,
                                ErrorType.InvalidFileType.ToString(),
                                null,
                                new { ErrorMessage = $"Invalid file extenstion: {extenstion}", Filename = file.FileName },
                                IpAddress,
                                UserAgent
                            );
                            return null;
                        }

                        if (file.ContentLength > 5 * 1024 * 1024) // 5MB Limit
                        {
                            _auditLogService.LogAudit(
                                userId,
                                ServiceRequestType,
                                ErrorType.FileSizeLimitExceeded.ToString(),
                                null,
                                new { ErrorMessage = $"File size exceeds 5MB: {file.ContentLength}", Filename = file.FileName },
                                IpAddress,
                                UserAgent
                            );
                            return null;
                        }

                        string uniqueFileName = $"{Guid.NewGuid()}{extenstion}";
                        string storagePath = $"{userFolder}/{uniqueFileName}"; // Store without request initially

                        byte[] fileBytes;
                        using (var memoryStream = new MemoryStream())
                        {
                            await file.InputStream.CopyToAsync(memoryStream);
                            fileBytes = memoryStream.ToArray();
                        }

                        var storageResponse = await _supabaseClient.Storage
                            .From(ServiceRequestDocumentBucket)
                            .Upload(fileBytes, storagePath, new Supabase.Storage.FileOptions
                            {
                                CacheControl = "3600",
                                Upsert = false,
                                
                            });

                        if (string.IsNullOrEmpty(storageResponse))
                        {
                            _auditLogService.LogAudit(
                                userId,
                                ServiceRequestType,
                                ErrorType.FileUploadError.ToString(),
                                null,
                                new { ErorrMessage = $"Failed to upload file: {file.FileName}", Filename = file.FileName },
                                IpAddress,
                                UserAgent
                            );
                            return null; // Fail if any upload fails
                        }

                        uploadedDocuments.Add(new ServiceRequestDocument
                        {
                            FileName = Path.GetFileName(file.FileName),
                            StoragePath = storagePath,
                            UploadedAt = DateTime.UtcNow,
                        });
                    }
                }

                // 4. Insert ServiceRequest only after successful file uploads
                var insertResponse = await _supabaseClient.From<ServiceRequest>().Insert(newRequest);
                if (insertResponse.Models == null || !insertResponse.Models.Any())
                {
                    _auditLogService.LogAudit(
                        userId,
                        ServiceRequestType,
                        ErrorType.InsertErrror.ToString(),
                        null,
                        new { ErrorMessage = $"Failed to insert service request: {insertResponse.ResponseMessage?.ReasonPhrase}" },
                        IpAddress,
                        UserAgent
                    );
                    // Clean uploaded files if request insertaion fails
                    foreach (var doc in uploadedDocuments)
                    {
                        await _supabaseClient.Storage.From(ServiceRequestDocumentBucket).Remove(doc.StoragePath);
                    }
                    return null;
                }

                var createdRequest = insertResponse.Models.First();
                int newRequestId = createdRequest.RequestId;

                // 5. Update document references with RequestId and insert
                if (uploadedDocuments.Any())
                {
                    foreach (var doc in uploadedDocuments)
                    {
                        // Update storage path to include requestId
                        var oldPath = doc.StoragePath;
                        var pathParts = oldPath.Split('/');
                        var newPath = $"{pathParts[0]}/{newRequestId}/{Path.GetFileName(oldPath)}";

                        await _supabaseClient.Storage.From(ServiceRequestDocumentBucket).Move(oldPath, newPath);
                        doc.StoragePath = newPath;
                        doc.RequestId = newRequestId;
                    }

                    var docInsertResponse = await _supabaseClient.From<ServiceRequestDocument>().Insert(uploadedDocuments);
                    if (docInsertResponse.Models == null || !docInsertResponse.Models.Any())
                    {
                        _auditLogService.LogAudit(
                            userId,
                            ServiceRequestType,
                            ErrorType.DocumentInsertError.ToString(),
                            newRequestId,
                            new { ErrorMessage = $"Failed to insert document references for request {newRequestId}: {docInsertResponse.ResponseMessage?.ReasonPhrase}" },
                            IpAddress,
                            UserAgent
                        );

                        // Cleanup: Delete the service request and uploaded files
                        await _supabaseClient.From<ServiceRequest>().Where(x => x.RequestId == newRequestId).Delete();
                        foreach (var doc in uploadedDocuments)
                        {
                            await _supabaseClient.Storage.From(ServiceRequestDocumentBucket).Remove(doc.StoragePath);
                        }
                        return null;
                    }
                }

                _auditLogService.LogAudit(
                    userId,
                    ServiceRequestType,
                    ErrorType.Created.ToString(),
                    newRequestId,
                    new { Message = "Service request created successfully", RequestId = newRequestId, DocumentCount = uploadedDocuments.Count },
                    IpAddress,
                    UserAgent
                );

                return newRequestId;
            }
            catch (UnauthorizedAccessException ex)
            {
                _auditLogService.LogAudit(
                    userId,
                    "ServiceRequest",
                    "AuthenticationError",
                    null,
                    new { ErrorMessage = ex.Message, StackTrace = ex.StackTrace },
                    IpAddress,
                    UserAgent
                );
                return null;
            }
            catch (Exception ex)
            {
                _auditLogService.LogAudit(
                    userId,
                    ServiceRequestType,
                    ErrorType.UnexpectedError.ToString(),
                    null,
                    new { ErrorMessage = ex.Message, StackTrace = ex.StackTrace },
                    IpAddress,
                    UserAgent
                );
                return null;
            }
        }
    }

    enum ErrorType 
    {
        InvalidInput,
        AuthenticationError,
        InvalidFile,
        InvalidFileType,
        FileSizeLimitExceeded,
        FileUploadError,
        InsertErrror,
        DocumentInsertError,
        Created,
        UnexpectedError
    }
}
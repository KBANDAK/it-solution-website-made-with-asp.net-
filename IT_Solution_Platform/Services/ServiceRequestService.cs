using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using IT_Solution_Platform.Handlers;
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
        private readonly ServiceRequestHandlerFactory _handleFactory;
        private readonly FileUploadService _fileUploadService;
        private readonly Supabase.Client _supabaseClient;

        // Remove the instance client - use ClientManager instead
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public ServiceRequestService()
        {
            _databaseService = new SupabaseDatabase();
            _auditLogService = new AuditLogService(_databaseService);
            _supabaseClient = SupabaseConfig.GetServiceClient();
            _handleFactory = new ServiceRequestHandlerFactory();
            _fileUploadService = new FileUploadService(_supabaseClient, _auditLogService);
        }

        private const string ServiceRequestType = "ServiceRequest";


        /// <summary>
        /// Unified method to create a service request for different service types.
        /// </summary>
        /// <param name="model">The view model (PenTestingRequestViewModel or MobileWebAppRequestViewModel).</param>
        /// <param name="userId">The ID of the user making the request.</param>
        /// <param name="accessToken">The access token for authentication.</param>
        /// <returns>The ID of the created service request, or null if creation fails.</returns>

        public async Task<int?> CreateServiceRequestAsync(object model, int userId, string accessToken = null)
        {
            if (model == null)
            {
                _auditLogService.LogAudit(userId, ServiceRequestType, ErrorType.InvalidInput.ToString(),
                    null, new { ErrorMessage = "Model is null" }, IpAddress, UserAgent);
                return null;
            }

            try
            {
                // 1. Get apropriate handler for the service type
                var handler = _handleFactory.GetHandler(model.GetType());
                if (handler == null)
                {
                    _auditLogService.LogAudit(userId, ServiceRequestType, ErrorType.InvalidInput.ToString(),
                         null, new { ErrorMessage = "Unsupported model type" }, IpAddress, UserAgent);
                    return null;
                }

                // 2. Validate the model
                if (!handler.ValidateModel(model, out string validationError))
                {
                    _auditLogService.LogAudit(userId, ServiceRequestType, ErrorType.InvalidInput.ToString(),
                        null, new { ErrorMessage = validationError }, IpAddress, UserAgent);
                    return null;
                }

                // 3. Extract request details using the handler
                var requestDetails = handler.ExtractRequestDetails(model);
                var supportingDocuments = handler.GetSupportingDocuments(model);
                var serviceId = handler.GetServiceId(model);

                // 4. Create service request
                var newRequest = new ServiceRequest
                {
                    UserId = userId,
                    ServiceId = serviceId,
                    StatusId = 1,
                    RequestDetails = JsonConvert.SerializeObject(requestDetails),
                    RequestedDate = DateTime.UtcNow,
                    Notes = "Submitted via web form",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // 5. Handle file uploads (can handle multiple files)
                var uploadedDocuments = new List<ServiceRequestDocument>();
                if (supportingDocuments != null && supportingDocuments.Any())
                {
                    try
                    {
                        uploadedDocuments = await _fileUploadService.UploadDocumentsAsync(
                            supportingDocuments, userId, accessToken, IpAddress, UserAgent);

                        _auditLogService.LogAudit(userId, ServiceRequestType, "FileUploadSuccess",
                            null, new { Message = $"Successfully uploaded {uploadedDocuments.Count} files" }, IpAddress, UserAgent);
                    }
                    catch (Exception ex)
                    {
                        _auditLogService.LogAudit(userId, ServiceRequestType, ErrorType.FileUploadError.ToString(),
                            null, new { ErrorMessage = ex.Message, FileCount = supportingDocuments.Length }, IpAddress, UserAgent);
                        return null;
                    }
                }

                // 6. Insert service request
                var insertResponse = await _supabaseClient.From<ServiceRequest>().Insert(newRequest);
                if (insertResponse.Models == null || !insertResponse.Models.Any())
                {
                    // Cleanup uploaded files if service request creation fails
                    if (uploadedDocuments.Any())
                    {
                        await _fileUploadService.CleanupDocumentsAsync(uploadedDocuments);
                    }
                    await _fileUploadService.CleanupDocumentsAsync(uploadedDocuments);
                    _auditLogService.LogAudit(userId, ServiceRequestType, ErrorType.InsertErrror.ToString(),
                        null, new { ErrorMessage = "Failed to insert service request" }, IpAddress, UserAgent);
                    return null;
                }

                var createdRequest = insertResponse.Models.First();
                int newRequestId = createdRequest.RequestId;

                // 7. Update document references and insert document records
                if (uploadedDocuments.Any())
                {
                    try
                    {
                        // Update storage paths to include request ID
                        await _fileUploadService.UpdateDocumentPathsAsync(uploadedDocuments, newRequestId);

                        // Insert document records into database
                        var docInsertResponse = await _supabaseClient.From<ServiceRequestDocument>().Insert(uploadedDocuments);
                        if (docInsertResponse.Models == null || !docInsertResponse.Models.Any())
                        {
                            // Rollback: Delete the service request and cleanup files
                            await _supabaseClient.From<ServiceRequest>().Where(x => x.RequestId == newRequestId).Delete();
                            await _fileUploadService.CleanupDocumentsAsync(uploadedDocuments);

                            _auditLogService.LogAudit(userId, ServiceRequestType, ErrorType.DocumentInsertError.ToString(),
                                newRequestId, new { ErrorMessage = "Failed to insert document references", DocumentCount = uploadedDocuments.Count }, IpAddress, UserAgent);
                            return null;
                        }

                        _auditLogService.LogAudit(userId, ServiceRequestType, "DocumentInsertSuccess",
                            newRequestId, new { Message = $"Successfully inserted {uploadedDocuments.Count} document references" }, IpAddress, UserAgent);
                    }
                    catch (Exception docEx)
                    {
                        // Rollback: Delete the service request and cleanup files
                        await _supabaseClient.From<ServiceRequest>().Where(x => x.RequestId == newRequestId).Delete();
                        await _fileUploadService.CleanupDocumentsAsync(uploadedDocuments);

                        _auditLogService.LogAudit(userId, ServiceRequestType, ErrorType.DocumentProcessingError.ToString(),
                            newRequestId, new { ErrorMessage = docEx.Message, DocumentCount = uploadedDocuments.Count }, IpAddress, UserAgent);
                        return null;
                    }
                }



                _auditLogService.LogAudit(userId, ServiceRequestType, ErrorType.Created.ToString(),
                    newRequestId, new { Message = "Service request created successfully", RequestId = newRequestId, DocumentCount = uploadedDocuments.Count }, IpAddress, UserAgent);

                return newRequestId;
            }
            catch (Exception ex)
            {
                _auditLogService.LogAudit(userId, ServiceRequestType, ErrorType.UnexpectedError.ToString(),
                    null, new { ErrorMessage = ex.Message, StackTrace = ex.StackTrace }, IpAddress, UserAgent);
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
        UnexpectedError,
        DocumentProcessingError
    }
}
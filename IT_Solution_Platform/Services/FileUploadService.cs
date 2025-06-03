using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using IT_Solution_Platform.Models;

namespace IT_Solution_Platform.Services
{
    public class FileUploadService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly AuditLogService _auditLogService;
        private const string ServiceRequestDocumentBucket = "service-request-documents";

        public FileUploadService(Supabase.Client supabaseClient, AuditLogService auditLogService)
        {
            _supabaseClient = supabaseClient;
            _auditLogService = auditLogService;
        }

        public async Task<List<ServiceRequestDocument>> UploadDocumentsAsync(
            HttpPostedFileBase[] files,
            int userId,
            string accessToken,
            string ipAddress,
            string userAgent)
        {
            var uploadedDocuments = new List<ServiceRequestDocument>();

            if (files == null || !files.Any())
                return uploadedDocuments;

            // Filter out null or empty files
            var validFiles = files.Where(f => f != null && f.ContentLength > 0).ToArray();

            if (!validFiles.Any())
            {
                _auditLogService.LogAudit(userId, "ServiceRequest", "NoValidFilesToUpload",
                    null, new { Message = "No valid files found to upload", TotalFiles = files.Length }, ipAddress, userAgent);
                return uploadedDocuments;
            }

           string userFolder = !string.IsNullOrEmpty(accessToken)
                ? (_supabaseClient.Auth.CurrentUser?.Id ?? $"user_{userId}")
                : $"user_{userId}";

            var failedUploads = new List<string>();
            var successfulUploads = new List<ServiceRequestDocument>();

            // Process each file
            for (int i = 0; i < validFiles.Length; i++)
            {
                var file = validFiles[i];
                try 
                {
                    _auditLogService.LogAudit(userId, "ServiceRequest", "FileUploadStarted",
                       null, new
                       {
                           FileName = file.FileName,
                           FileSize = file.ContentLength,
                           FileIndex = i + 1,
                           TotalFiles = validFiles.Length
                       }, ipAddress, userAgent);

                    var validationResult = ValidateFile(file, userId, ipAddress, userAgent);
                    if (!validationResult.IsValid)
                    {
                        failedUploads.Add($"{file.FileName}: {validationResult.ErrorMessage}");
                        continue;
                    }

                    var uploadedDoc = await UploadSingleFileAsync(file, userFolder);
                    successfulUploads.Add(uploadedDoc);

                    _auditLogService.LogAudit(userId, "ServiceRequest", "FileUploadCompleted",
                        null, new
                        {
                            FileName = file.FileName,
                            StoragePath = uploadedDoc.StoragePath,
                            FileIndex = i + 1,
                            TotalFiles = validFiles.Length
                        }, ipAddress, userAgent);
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Failed to upload file '{file.FileName}': {ex.Message}";
                    failedUploads.Add(errorMessage);

                    _auditLogService.LogAudit(userId, "ServiceRequest", ErrorType.FileUploadError.ToString(),
                        null, new
                        {
                            FileName = file.FileName,
                            ErrorMessage = ex.Message,
                            FileIndex = i + 1,
                            TotalFiles = validFiles.Length
                        }, ipAddress, userAgent);
                }
            }

            // Log summary
            _auditLogService.LogAudit(userId, "ServiceRequest", "FileUploadSummary",
                null, new
                {
                    TotalFiles = validFiles.Length,
                    SuccessfulUploads = successfulUploads.Count,
                    FailedUploads = failedUploads.Count,
                    FailedFiles = failedUploads
                }, ipAddress, userAgent);


            // If any files failed to upload, throw an exception with details
            if (failedUploads.Any())
            {
                // Cleanup successful uploads since we're failing the entire operation
                await CleanupDocumentsAsync(successfulUploads);

                var errorMessage = $"Failed to upload {failedUploads.Count} out of {validFiles.Length} files. Details: {string.Join("; ", failedUploads)}";
                throw new InvalidOperationException(errorMessage);
            }

            return successfulUploads;
        }

        private (bool IsValid, string ErrorMessage) ValidateFile(HttpPostedFileBase file, int userId, string ipAddress, string userAgent)
        {
            if (file == null || file.ContentLength == 0)
            {
                _auditLogService.LogAudit(userId, "ServiceRequest", ErrorType.InvalidFile.ToString(),
                    null, new { ErrorMessage = "Null or empty file provided", Filename = file?.FileName }, ipAddress, userAgent);
                return (false, "Invalid file provided");
            }

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".jpg", ".jpeg", ".png", ".gif", ".xlsx", ".xls" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                _auditLogService.LogAudit(userId, "ServiceRequest", ErrorType.InvalidFileType.ToString(),
                    null, new { ErrorMessage = $"Invalid file extension: {extension}", Filename = file.FileName }, ipAddress, userAgent);
                return (false, $"Invalid file extension: {extension}. Allowed extensions: {string.Join(", ", allowedExtensions)}");
            }

            if (file.ContentLength > 10 * 1024 * 1024) // 10MB Limit (increased from 5MB)
            {
                _auditLogService.LogAudit(userId, "ServiceRequest", ErrorType.FileSizeLimitExceeded.ToString(),
                    null, new { ErrorMessage = $"File size exceeds 10MB: {file.ContentLength}", Filename = file.FileName }, ipAddress, userAgent);
                return (false, $"File size exceeds 10MB limit. File size: {file.ContentLength / (1024 * 1024)}MB");
            }

            return (true, string.Empty);
        }

        private async Task<ServiceRequestDocument> UploadSingleFileAsync(HttpPostedFileBase file, string userFolder)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            string uniqueFileName = $"{Guid.NewGuid()}{extension}";
            string storagePath = $"{userFolder}/{uniqueFileName}";

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                file.InputStream.Position = 0; // Reset stream position
                await file.InputStream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            var storageResponse = await _supabaseClient.Storage
                .From(ServiceRequestDocumentBucket)
                .Upload(fileBytes, storagePath, new Supabase.Storage.FileOptions
                {
                    CacheControl = "3600",
                    Upsert = false
                });

            if (string.IsNullOrEmpty(storageResponse))
                throw new InvalidOperationException($"Failed to upload file: {file.FileName}");

            return new ServiceRequestDocument
            {
                FileName = Path.GetFileName(file.FileName),
                StoragePath = storagePath,
                UploadedAt = DateTime.UtcNow
            };
        }

        public async Task CleanupDocumentsAsync(List<ServiceRequestDocument> documents)
        {
            if (documents == null || !documents.Any())
                return;

            var cleanupTasks = documents.Select(async doc =>
            {
                try
                {
                    await _supabaseClient.Storage.From(ServiceRequestDocumentBucket).Remove(doc.StoragePath);
                    return new { Success = true, Document = doc.FileName, Error = (string)null };
                }
                catch (Exception ex)
                {
                    return new { Success = false, Document = doc.FileName, Error = ex.Message };
                }
            });

            var results = await Task.WhenAll(cleanupTasks);

            // Log cleanup results
            var successful = results.Count(r => r.Success);
            var failed = results.Count(r => !r.Success);

            if (failed > 0)
            {
                var failedDocs = results.Where(r => !r.Success).Select(r => $"{r.Document}: {r.Error}");
                // Note: We can't log here as we don't have userId, ipAddress, userAgent context
                // Consider adding these parameters if detailed cleanup logging is needed
            }
        }

        public async Task UpdateDocumentPathsAsync(List<ServiceRequestDocument> documents, int requestId)
        {
            if (documents == null || !documents.Any())
                return;

            var updateTasks = documents.Select(async doc =>
            {
                try
                {
                    var oldPath = doc.StoragePath;
                    var pathParts = oldPath.Split('/');
                    var fileName = Path.GetFileName(oldPath);
                    var newPath = $"{pathParts[0]}/{requestId}/{fileName}";

                    await _supabaseClient.Storage.From(ServiceRequestDocumentBucket).Move(oldPath, newPath);

                    doc.StoragePath = newPath;
                    doc.RequestId = requestId;

                    return new { Success = true, Document = doc.FileName, OldPath = oldPath, NewPath = newPath, Error = (string)null };
                }
                catch (Exception ex)
                {
                    return new { Success = false, Document = doc.FileName, OldPath = doc.StoragePath, NewPath = (string)null, Error = ex.Message };
                }
            });

            var results = await Task.WhenAll(updateTasks);

            var failed = results.Where(r => !r.Success).ToList();
            if (failed.Any())
            {
                var errorMessage = $"Failed to update paths for {failed.Count} documents: {string.Join("; ", failed.Select(f => $"{f.Document} - {f.Error}"))}";
                throw new InvalidOperationException(errorMessage);
            }
        }
    }
}
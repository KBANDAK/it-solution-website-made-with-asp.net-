using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using IT_Solution_Platform.Helpers;
using IT_Solution_Platform.Models;
using IT_Solution_Platform.Services;
using Newtonsoft.Json;

namespace IT_Solution_Platform.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ServiceRequestService _serviceRequestService;
        private readonly AuditLogService _auditLogService;
        private readonly SupabaseDatabase _database;
        private SupabaseAuthService _supabaseAuth;

        public ProfileController()
        {
            _database = new SupabaseDatabase();
            _serviceRequestService = new ServiceRequestService();
            _auditLogService = new AuditLogService(_database);
        }


        public SupabaseAuthService GetAuthService() 
        {
            if (_supabaseAuth == null) 
            {
                _supabaseAuth = new SupabaseAuthService();
            }
            return _supabaseAuth;
        }
        /// <summary>
        /// Action for displaying user's service orders with enhanced security and full data retrieval
        /// </summary>
        [HttpGet]
        [ActionName("ServiceOrders")]
        public async Task<ActionResult> ServiceOrdersAsync()
        {
            try
            {
                // Enhanced user ID extraction with multiple fallbacks
                var userIdClaim = GetUserIdFromClaims();

                if (!userIdClaim.HasValue)
                {
                    _auditLogService.LogAudit(0, "ServiceOrders", "InvalidUserClaim", null,
                        new { Message = "Failed to extract user ID from claims" },
                        Request.UserHostAddress, Request.UserAgent);

                    TempData["Error"] = "Unable to verify your identity. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var userId = userIdClaim.Value;

                // Fetch comprehensive service requests with enhanced data
                var serviceRequests = await _serviceRequestService.GetUserServiceRequestsAsync(userId);

                // Additional security: Filter out any sensitive data for display
                var sanitizedRequests = SanitizeServiceRequestsForDisplay(serviceRequests);

                // Success audit log
                _auditLogService.LogAudit(userId, "ServiceOrders", "ViewSuccess", null,
                    new
                    {
                        Message = $"User successfully viewed {sanitizedRequests.Count} service orders",
                        RequestCount = sanitizedRequests.Count
                    },
                    Request.UserHostAddress, Request.UserAgent);

                ViewBag.UserName = GetUserDisplayName();
                ViewBag.UserId = userId;

                return View("ServiceOrdersAsync", sanitizedRequests);
            }
            catch (UnauthorizedAccessException ex)
            {
                _auditLogService.LogAudit(0, "ServiceOrders", "SecurityViolation", null,
                    new { ErrorMessage = ex.Message, StackTrace = ex.StackTrace },
                    Request.UserHostAddress, Request.UserAgent);

                TempData["Error"] = "Access denied. Please contact support if this error persists.";
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                var userId = GetUserIdFromClaims() ?? 0;
                _auditLogService.LogAudit(userId, "ServiceOrders", "UnexpectedError", null,
                    new { ErrorMessage = ex.Message, StackTrace = ex.StackTrace },
                    Request.UserHostAddress, Request.UserAgent);

                TempData["Error"] = "An error occurred while loading your service orders. Please try again.";
                return View("ServiceOrdersAsync", new List<ServiceRequestDetailViewModel>());
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            try
            {
                if (!Request.IsAuthenticated)
                {
                    return Json(new { success = false, message = "You must be logged in to change your password." });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                // Additional server-side validation
                if (model.CurrentPassword == model.NewPassword)
                {
                    return Json(new { success = false, message = "New password must be different from current password." });
                }

                // Get current user info
                var userEmail = User.Identity.Name;
                var userId = GetUserIdFromClaims() ?? 0; 
                var supbaseUid = GetSubabaseUidFromClaims();

                if (string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(supbaseUid))
                {
                    return Json(new { success = false, message = "Unable to retrieve user information." });
                }

                // Verify current password by attempting to sign in
                var signInResult = await GetAuthService().SignInWithEmailAsync(userEmail, model.CurrentPassword);
                if (string.IsNullOrEmpty(signInResult.AccessToken))
                {
                    _auditLogService.LogAudit(
                        userId,
                        "Password Change Failed - Invalid Current Password",
                        "User",
                        null,
                        new { Email = userEmail },
                        Request.UserHostAddress,
                        Request.UserAgent
                    );
                    return Json(new { success = false, message = "Current password is incorrect." });
                }

                // Update password using the access token from sign-in
                var updateResult = await _supabaseAuth.ResetUserPasswordAsync(
                    supbaseUid,
                    model.NewPassword
                );

                if (updateResult.Success)
                {
                    _auditLogService.LogAudit(
                        userId,
                        "Password Changed Successfully",
                        "User",
                        null,
                        new { Email = userEmail },
                        Request.UserHostAddress,
                        Request.UserAgent
                    );
                    await GetAuthService().SignOutAsync();

                    return Json(new { success = true, message = "Password updated successfully!" });
                }
                else
                {
                    _auditLogService.LogAudit(
                        userId,
                        "Password Change Failed",
                        "User",
                        null,
                        new { Email = userEmail, Error = updateResult.Message },
                        Request.UserHostAddress,
                        Request.UserAgent
                    );

                    return Json(new { success = false, message = updateResult.Message ?? "Failed to update password." });
                }
            }
            catch (Exception ex)
            {
                var userId = GetUserIdFromClaims() ?? 0;
                _auditLogService.LogAudit(
                    userId,
                    "Password Change Error",
                    "User",
                    null,
                    new { Error = ex.Message },
                    Request.UserHostAddress,
                    Request.UserAgent
                );

                return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DownloadReceipt(string orderData)
        {
            try
            {
                // Parse the order data
                var order = JsonConvert.DeserializeObject<ServiceRequestDetailViewModel>(orderData);

                // Generate PDF using the helper class
                var pdfBytes = PdfGenerator.GenerateReceiptPdf(order);

                // Return PDF file with a descriptive filename
                var fileName = $"Secudev_Receipt_{order.request_id:D6}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (JsonException jsonEx)
            {
                // Log JSON parsing error
                // Logger.Error($"JSON parsing error in DownloadReceipt: {jsonEx.Message}", jsonEx);
                return new HttpStatusCodeResult(400, "Invalid order data format");
            }
            catch (Exception ex)
            {
                // Log general error
                // Logger.Error($"Error generating receipt for user Moody03: {ex.Message}", ex);
                return new HttpStatusCodeResult(500, "Error generating receipt. Please try again.");
            }
        }

        /// <summary>
        /// Enhanced method to extract user ID from claims with multiple fallbacks
        /// </summary>
        private int? GetUserIdFromClaims()
        {
            if (!(User.Identity is ClaimsIdentity identity))
                return null;

            // Try multiple claim types for user ID
            var userIdClaim = identity.FindFirst("UserId")?.Value ??
                             identity.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                             identity.FindFirst("user_id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return null;

            return int.TryParse(userIdClaim, out int userId) ? userId : (int?)null;
        }

        private string GetSubabaseUidFromClaims()
        {
            if (!(User.Identity is ClaimsIdentity identity))
                return null;

            // Try multiple claim types for user ID
            var userIdClaim = identity.FindFirst("SupabaseUserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return null;

            return userIdClaim;
        }



        /// <summary>
        /// Get user display name for UI
        /// </summary>
        private string GetUserDisplayName()
        {
            if (!(User.Identity is ClaimsIdentity identity))
                return "User";

            var firstName = identity.FindFirst("FirstName")?.Value ?? "";
            var lastName = identity.FindFirst("LastName")?.Value ?? "";

            if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
                return $"{firstName} {lastName}".Trim();

            return identity.FindFirst(ClaimTypes.Email)?.Value ?? "User";
        }

        /// <summary>
        /// Sanitize service requests for display (remove sensitive internal data)
        /// </summary>
        private List<ServiceRequestDetailViewModel> SanitizeServiceRequestsForDisplay(List<ServiceRequestDetailViewModel> requests)
        {
            foreach (var request in requests)
            {
                if (!string.IsNullOrEmpty(request.notes))
                {
                    request.notes = FilterInternalNotes(request.notes);
                }
            }

            return requests;
        }

        /// <summary>
        /// Filter out internal notes that shouldn't be shown to end users
        /// </summary>
        private string FilterInternalNotes(string notes)
        {
            // Example: Remove notes that start with "INTERNAL:" or similar patterns
            if (string.IsNullOrEmpty(notes))
                return notes;

            var lines = notes.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var filteredLines = lines.Where(line =>
                !line.Trim().StartsWith("INTERNAL:", StringComparison.OrdinalIgnoreCase) &&
                !line.Trim().StartsWith("ADMIN:", StringComparison.OrdinalIgnoreCase) &&
                !line.Trim().StartsWith("STAFF:", StringComparison.OrdinalIgnoreCase)
            );

            return string.Join("\n", filteredLines);
        }
    }
}
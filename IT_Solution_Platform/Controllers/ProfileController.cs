using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using IT_Solution_Platform.Models;
using IT_Solution_Platform.Services;

namespace IT_Solution_Platform.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ServiceRequestService _serviceRequestService;
        private readonly AuditLogService _auditLogService;
        private readonly SupabaseDatabase _database;

        public ProfileController()
        {
            _database = new SupabaseDatabase();
            _serviceRequestService = new ServiceRequestService();
            _auditLogService = new AuditLogService(_database);
        }

        /// <summary>
        /// Action for displaying user's service orders with enhanced security and full data retrieval
        /// </summary>
        [HttpGet]
        [ActionName("ServiceOrders")]
        public async Task<ActionResult> ServiceOrdersAsync()
        {
            // Enhanced authentication check
            if (!User.Identity.IsAuthenticated)
            {
                _auditLogService.LogAudit(0, "ServiceOrders", "UnauthorizedAccess", null,
                    new { Message = "Unauthenticated user attempted to access service orders" },
                    Request.UserHostAddress, Request.UserAgent);

                ViewBag.ErrorMessage = "You must be logged in to view your service orders.";
                return RedirectToAction("Login", "Account");
            }

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
                // Remove or mask sensitive data that shouldn't be shown to end users
                // For example, internal notes might contain sensitive information
                if (!string.IsNullOrEmpty(request.notes))
                {
                    // You might want to filter out internal notes or mask certain content
                    // This is just an example - adjust based on your business rules
                    request.notes = FilterInternalNotes(request.notes);
                }

                // Ensure approved_by is not exposing internal user IDs inappropriately
                // You might want to replace with approver name instead of ID
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
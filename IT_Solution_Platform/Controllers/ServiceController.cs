using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using IT_Solution_Platform.Models;
using IT_Solution_Platform.Services;
using Supabase;

namespace IT_Solution_Platform.Controllers
{
    [Authorize]
    public class ServiceController : Controller
    {
        private readonly SupabaseDatabase _databaseService;
        private readonly SupabaseAuthService _authService;
        private readonly ServiceRequestService _serviceRequestService;
        private readonly AuditLogService _logService;

        // Constructor without Supabase Client injection
        public ServiceController(ServiceRequestService serviceRequestService, SupabaseDatabase databaseService, SupabaseAuthService authService, AuditLogService logService) 
        {
            _serviceRequestService = serviceRequestService ?? throw new ArgumentNullException(nameof(serviceRequestService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        // GET: /Service/PenTesting
        [HttpGet]
        public ActionResult PenTesting()
        {
            var viewModel = new PenTestingRequestViewModel
            {
                ServiceId = GetPenetrationTestingServiceId(),
                PrimaryContactName = GetCurrentUserFullName(),
                PrimaryContactEmail = GetCurrentUserEmail()
            };

            return View(viewModel);
        }

        // POST: /Service/PenTesting
        [HttpPost]
        
        public async Task<ActionResult> PenTesting(PenTestingRequestViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            /*if (viewModel.SupportingDocuments != null)
            {
                foreach (var file in viewModel.SupportingDocuments)
                {
                    if (file.ContentLength > 5 * 1024 * 1024) // 5MB in bytes
                    {
                        ModelState.AddModelError("SupportingDocuments", $"File {file.FileName} exceeds the 5MB limit.");
                        return View(viewModel);
                    }
                }
            }*/
            try
            {
                var userId = GetCurrentUserId();
                var accessToken = Request.Cookies["access_token"]?.Value;
                var refershToken = Request.Cookies["refresh_token"]?.Value;
                if (userId == null)
                {
                    ModelState.AddModelError(string.Empty, "User not found. Please log in again.");
                    return View(viewModel);
                }

                viewModel.ServiceId = GetPenetrationTestingServiceId();
                var newRequestId = await _serviceRequestService.CreatePenTestingServiceRequestAsync(viewModel, userId.Value, accessToken);

                if (newRequestId.HasValue)
                {
                    TempData["SuccessMessage"] = $"Your Penetration Testing request (ID: {newRequestId.Value}) has been submitted successfully.";
                    return RedirectToAction("RequestSubmitted");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Failed to submit the request. Please try again.");
                }
            }
            catch (Exception ex)
            {
                // Replace with proper logging
                Console.WriteLine($"Error in PenTesting POST: {ex}");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
            }

            return View(viewModel);
        }

        [HttpGet]
        public ActionResult RequestSubmitted()
        {
            return View();
        }

        // --- Placeholder Methods (Replace with your actual implementation) ---

        private int GetPenetrationTestingServiceId()
        {
            try
            {
                int result = (int) _databaseService.ExecuteQuerySingle<int>("SELECT service_id FROM services WHERE LOWER(name) = @name", new { name = "penetration testing" });
                return result;
            }
            catch (Exception ex)
            {
                _logService.LogAudit(
                    userId: GetCurrentUserId() ?? 0,
                    action: "Get Penetration Testing Service ID",
                    entityType: "Service",
                    entityId: null,
                    details: new { error = ex.Message },
                    ipAddress: Request.UserHostAddress,
                    userAgent: Request.UserAgent);
                return 0;
            }
        }

        private int? GetCurrentUserId()
        {
            try
            {
                if (User.Identity.IsAuthenticated)
                {
                    // Cast User to ClaimsPrincipal to access claims
                    if (User is ClaimsPrincipal claimsPrincipal)
                    {
                        // Get the user_id claim that was set in the JWT filter
                        var userIdClaim = claimsPrincipal.FindFirst("user_id")?.Value;
                        if (!string.IsNullOrEmpty(userIdClaim))
                        {
                            // Parse the user_id claim directly since it's already the database user_id
                            if (int.TryParse(userIdClaim, out int userId))
                            {
                                return userId;
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logService.LogAudit(
                    userId: 0,
                    action: "Get Current User ID",
                    entityType: "User",
                    entityId: null,
                    details: new { error = ex.Message },
                    ipAddress: Request.UserHostAddress,
                    userAgent: Request.UserAgent);
                return null;
            }
        }

        private string GetCurrentUserFullName()
        {
            try
            {
                if (User.Identity.IsAuthenticated)
                {
                    // Check session first (set by JwtAuthenticationFilter)
                    if (HttpContext.Session["Fullname"] != null)
                    {
                        return HttpContext.Session["Fullname"].ToString();
                    }

                    // Cast User to ClaimsPrincipal to access claims
                    if (User is ClaimsPrincipal claimsPrincipal)
                    {
                        // Fallback to database query using NameIdentifier (Supabase UID)
                        var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (!string.IsNullOrEmpty(userIdClaim))
                        {
                            var userDetails = _databaseService.ExecuteQuery<User>(
                                "SELECT first_name, last_name FROM users WHERE supabase_uid = CAST(@supabaseUid AS uuid)",
                                new { supabaseUid = userIdClaim }).FirstOrDefault();

                            if (userDetails != null)
                            {
                                var fullName = $"{userDetails.first_name} {userDetails.last_name}".Trim();
                                HttpContext.Session["Fullname"] = fullName;
                                return fullName;
                            }
                        }
                    }

                    return User.Identity.Name; // Fallback to email
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user full name: {ex}");
                return string.Empty;
            }
        }

        private string GetCurrentUserEmail()
        {
            try
            {
                if (User.Identity.IsAuthenticated)
                {
                    // Cast User to ClaimsPrincipal to access claims
                    if (User is ClaimsPrincipal claimsPrincipal)
                    {
                        // Try to get email from claims first
                        var emailClaim = claimsPrincipal.FindFirst("email")?.Value;
                        if (!string.IsNullOrEmpty(emailClaim))
                        {
                            return emailClaim;
                        }
                    }

                    // Fallback to Identity.Name (which should be the email)
                    return User.Identity.Name ?? string.Empty;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user email: {ex}");
                return string.Empty;
            }
        }
    }
}

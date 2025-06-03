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
        private readonly ServiceRequestService _serviceRequestService;
        private readonly AuditLogService _logService;

        // Constructor without Supabase Client injection
        public ServiceController(ServiceRequestService serviceRequestService, SupabaseDatabase databaseService, SupabaseAuthService authService, AuditLogService logService) 
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
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

            if (viewModel.ServiceId == 0) 
            {
                ViewBag.ErrorMessage = "Unable to load Penetration Testing service. Please try again later.";
                return View("Error");
            }

            return View(viewModel);
        }

        // POST: /Service/PenTesting
        [HttpPost]
        [ValidateAntiForgeryToken]
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
                if (userId == null)
                {
                    ViewBag.ErrorMessage = "User not found. Please log in again.";
                    return View("Error");
                }
                var accessToken = Request.Cookies["access_token"]?.Value;
                var refershToken = Request.Cookies["refresh_token"]?.Value;
                if (userId == null)
                {
                    ModelState.AddModelError(string.Empty, "User not found. Please log in again.");
                    return View(viewModel);
                }

                viewModel.ServiceId = GetPenetrationTestingServiceId();
                if (viewModel.ServiceId == 0)
                {
                    ViewBag.ErrorMessage = "Unable to load Penetration Testing service. Please try again later.";
                    return View("Error");
                }
                var newRequestId = await _serviceRequestService.CreateServiceRequestAsync(viewModel, userId.Value, accessToken);

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
                return View("Error");
            }

            return View(viewModel);
        }


        [HttpGet]
        public ActionResult MobileWebApp() 
        {
            var viewModel = new MobileWebAppRequestViewModel
            {
                ServiceId = GetMobileWebAppServiceId(), 
                PrimaryContactName = GetCurrentUserFullName(),
                PrimaryContactEmail = GetCurrentUserEmail()
            };

            if (viewModel.ServiceId == 0)
            {
                ViewBag.ErrorMessage = "Unable to load Mobile & Web Application service. Please try again later.";
                return View("Error");
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> MobileWebApp(MobileWebAppRequestViewModel viewModel)
        {
            if (!ModelState.IsValid) 
            { 
                return View(viewModel);
            }

            if (viewModel.SupportingDocuments != null)
            {
                foreach (var file in viewModel.SupportingDocuments)
                {
                    if (file != null) {
                        
                        if (file.ContentLength > 5 * 1024 * 1024) // 5MB in bytes
                        {
                            ModelState.AddModelError("SupportingDocuments", $"File {file.FileName} exceeds the 5MB limit.");
                            return View(viewModel);
                        }

                    }
                   
                }
            }

            try 
            {
                var userId = GetCurrentUserId();

                var accessToken = Request.Cookies["access_token"]?.Value;
                var refershToken = Request.Cookies["refresh_token"]?.Value;
                if (userId == null)
                {
                    ViewBag.ErrorMessage = "User not found. Please log in again.";
                    return View("Error");
                }
                viewModel.ServiceId = GetMobileWebAppServiceId();
                if (viewModel.ServiceId == 0)
                {
                    ViewBag.ErrorMessage = "Unable to load Mobile & Web Application service. Please try again later.";
                    return View("Error");
                }
                var newRequestId = await _serviceRequestService.CreateServiceRequestAsync(viewModel, userId.Value, accessToken);
                if (newRequestId.HasValue)
                {
                    TempData["SuccessMessage"] = $"Your Mobile Web Application request (ID: {newRequestId.Value}) has been submitted successfully.";
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
                Console.WriteLine($"Error in MobileWebApp POST: {ex}");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                ViewBag.ErrorMessage = "An unexpected error occurred while submitting your request. Please try again.";
                return View("Error");
            }
            return View(viewModel);
        }

        [HttpGet]
        public ActionResult NetworkService()
        {
            var viewModel = new NetworkServiceModel
            {
                ServiceId = GetNetworkServiceId(),
                PrimaryContactName = GetCurrentUserFullName(),
                PrimaryContactEmail = GetCurrentUserEmail()
            };

            if (viewModel.ServiceId == 0)
            {
                ViewBag.ErrorMessage = "Unable to load Network Service. Please try again later.";
                return View("Error");
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> NetworkService(NetworkServiceModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }
            try
            {
                var userId = GetCurrentUserId();
                var accessToken = Request.Cookies["access_token"]?.Value;
                var refershToken = Request.Cookies["refresh_token"]?.Value;
                if (userId == null)
                {
                    ViewBag.ErrorMessage = "User not found. Please log in again.";
                    return View("Error");
                }
                viewModel.ServiceId = GetNetworkServiceId();
                if (viewModel.ServiceId == 0)
                {
                    ViewBag.ErrorMessage = "Unable to load Network Service. Please try again later.";
                    return View("Error");
                }
                var newRequestId = await _serviceRequestService.CreateServiceRequestAsync(viewModel, userId.Value, accessToken);
                if (newRequestId.HasValue)
                {
                    TempData["SuccessMessage"] = $"Your Network Service request (ID: {newRequestId.Value}) has been submitted successfully.";
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
                Console.WriteLine($"Error in NetworkService POST: {ex}");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                ViewBag.ErrorMessage = "An unexpected error occurred while submitting your request. Please try again.";
                return View("Error");
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
                int result = (int) _databaseService.ExecuteQuerySingle<int>("SELECT service_id FROM services WHERE LOWER(TRIM(name)) = LOWER(TRIM(@name))", new { name = "penetration testing" });
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

        private int GetMobileWebAppServiceId()
        {
            try
            {
                int result = (int)_databaseService.ExecuteQuerySingle<int>("SELECT service_id FROM services WHERE LOWER(TRIM(name)) = LOWER(TRIM(@name))", new { name = "mobile & Web application" });
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

        private int GetNetworkServiceId()
        {
            try
            {
                int result = (int)_databaseService.ExecuteQuerySingle<int>("SELECT service_id FROM services WHERE LOWER(TRIM(name)) = LOWER(TRIM(@name))", new { name = "network service" });
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
                        var userIdClaim = claimsPrincipal.FindFirst("UserId")?.Value;
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

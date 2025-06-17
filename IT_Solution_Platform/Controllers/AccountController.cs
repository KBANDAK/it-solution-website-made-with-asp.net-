using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Web.Services;
using IT_Solution_Platform.Models;
using IT_Solution_Platform.Services;
using Supabase.Gotrue;
using Supabase.Interfaces;

namespace IT_Solution_Platform.Controllers
{
    /// <summary>
    /// Handles user authentication and account management operations including:
    /// - User registration and sign-up
    /// - Login authentication
    /// - Email verification
    /// - Password management (reset, change)
    /// - Account session management
    /// </summary>
    public class AccountController : Controller
    {
        private readonly AuditLogService _auditLogService;
        private readonly SupabaseAuthService _authService;
        private readonly SupabaseDatabase _databaseService;



        public AccountController(AuditLogService auditLogService)

        {
            _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
            _authService = new SupabaseAuthService();
            _databaseService = new SupabaseDatabase();
        }

        #region Registration
        [HttpGet]
        [AllowAnonymous]
        [OutputCache(Duration = 0, NoStore = true)]
        public ActionResult Register()
        {

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [OutputCache(Duration = 0, NoStore = true)]
        public async Task<ActionResult> Register(SignUpViewModel model)
        {

            if (!ModelState.IsValid)
            {
                _auditLogService.LogAudit(0, "Registration Attempt", "User", null, new { Email = model.Email, Error = "Invalid ModelState" }, Request.UserHostAddress, Request.UserAgent);
                return View(model);
            }

            try
            {
                // Check if user already exists
                var existingUser = _databaseService.ExecuteQuery<Models.User>("SELECT * FROM users WHERE email = @Email", new { Email = model.Email }).FirstOrDefault();
                if (existingUser != null)
                {
                    _auditLogService.LogAudit(0, "Registration Attempt", "User", null, new { Email = model.Email, Error = "User already exists" }, Request.UserHostAddress, Request.UserAgent);
                    ModelState.AddModelError("", "Registration Failed. Please try again later.");
                    return View(model);
                }

                var redirectUrl = Url.Action("Verify", "Account", null, Request.Url.Scheme);
                var options = new SignUpOptions { RedirectTo = redirectUrl };

                // Register user with Supabase
                var (accessToken, message) = await _authService.SignUpAsync(
                    model.Email,
                    model.Password,
                    model.FirstName,
                    model.LastName,
                    model.PhoneNumber,
                    options
                );

                if (string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(message))
                {
                    _auditLogService.LogAudit(0, "Registration Failure", "User", null, new { Email = model.Email, Error = "Supabase sign-up failed" }, Request.UserHostAddress, Request.UserAgent);
                    ViewBag.ErrorMessage = "Failed to register. Please try again later.";
                    return View("Error");
                }

                // If access token is provided, user might be auto-verified
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var supabaseUser = await _authService.GetUserByTokenAsync(accessToken);
                    if (supabaseUser == null)
                    {
                        _auditLogService.LogAudit(0, "Registration Failure", "User", null, new { Email = model.Email, Error = "Invalid access token" }, Request.UserHostAddress, Request.UserAgent);
                        ViewBag.ErrorMessage = "Registration failed due to an invalid token. Please try again.";
                        return View("Error");
                    }

                    var dbUser = _databaseService.ExecuteQuery<Models.User>(
                        "SELECT * FROM users WHERE supabase_uid = CAST(@SupabaseUid AS uuid)",
                        new { SupabaseUid = supabaseUser.Id }).FirstOrDefault();

                    if (dbUser != null && (bool)dbUser.is_active)
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }

                ViewBag.Email = model.Email;
                ViewBag.Message = message;
                TempData["Email"] = model.Email;
                TempData["SuccessMessage"] = message;

                return RedirectToAction("Verify");
            }
            catch (Exception ex)
            {
                _auditLogService.LogAudit(0, "Registration Error", "User", null, new { Email = model.Email, Error = ex.Message }, Request.UserHostAddress, Request.UserAgent);
                ViewBag.ErrorMessage = "An unexpected error occurred during registration. Please try again later.";
                return View("Error");
            }
        }
        #endregion

        #region Verification
        [HttpGet]
        [AllowAnonymous]
        [ActionName("verify")]
        public async Task<ActionResult> VerificationAsync(string access_token = null, string type = null, string error = null, string error_code = null, string error_description = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(error))
                {
                    _auditLogService.LogAudit(0, "Verification Error", "User", null, new { Error = error, ErrorCode = error_code, ErrorDescription = error_description }, Request.UserHostAddress, Request.UserAgent);
                    ViewBag.Message = error_description?.Replace("+", " ") ?? "An error occurred during verification. Please request a new confirmation email.";
                    return View("Verification");
                }

                if (!string.IsNullOrEmpty(access_token) && type == "signup")
                {
                    var supabaseUser = await _authService.GetUserByTokenAsync(access_token);
                    if (supabaseUser == null)
                    {
                        _auditLogService.LogAudit(0, "Verification Failure", "User", null, new { Error = "Invalid or expired access token" }, Request.UserHostAddress, Request.UserAgent);
                        ViewBag.Message = "The verification link is invalid or expired. Please request a new confirmation email.";
                        return View("Verification");
                    }

                    var dbUser = _databaseService.ExecuteQuery<Models.User>("SELECT * FROM users WHERE supabase_uid = CAST(@SupabaseUid AS uuid)", new { SupabaseUid = supabaseUser.Id }).FirstOrDefault();
                    if (dbUser == null)
                    {
                        _auditLogService.LogAudit(0, "Verification Failure", "User", null, new { Email = supabaseUser.Email, Error = "User not found in database" }, Request.UserHostAddress, Request.UserAgent);
                        ViewBag.Message = "User not found. Please register again.";
                        return View("Verification");
                    }

                    if (!dbUser.is_active) // Simplified boolean check
                    {
                        var updateQuery = "UPDATE users SET is_active = @IsActive, updated_at = @UpdatedAt WHERE supabase_uid = CAST(@SupabaseUid AS uuid)";
                        var updateResult = _databaseService.ExecuteNonQuery(updateQuery, new
                        {
                            IsActive = true,
                            UpdatedAt = DateTime.UtcNow,
                            SupabaseUid = supabaseUser.Id
                        });

                        if (updateResult <= 0)
                        {
                            _auditLogService.LogAudit(0, "Verification Failure", "User", null, new { Email = supabaseUser.Email, Error = "Failed to update user status" }, Request.UserHostAddress, Request.UserAgent);
                            ViewBag.Message = "Verification failed due to a server error. Please try again later.";
                            return View("Verification");
                        }

                        // Refresh user data after update
                        dbUser.is_active = true;
                        dbUser.updated_at = DateTime.UtcNow;
                    }

                    _auditLogService.LogAudit(0, "Verification Success", "User", null, new { Email = supabaseUser.Email, SupabaseUid = supabaseUser.Id }, Request.UserHostAddress, Request.UserAgent);
                    return RedirectToAction("Index", "Home");
                }

                ViewBag.Email = TempData["Email"] ?? string.Empty;
                ViewBag.Message = TempData["SuccessMessage"] ?? "Please check your email for a confirmation link.";
                return View("Verification");
            }
            catch (Exception ex)
            {
                _auditLogService.LogAudit(0, "Verification Error", "User", null, new { Error = ex.Message }, Request.UserHostAddress, Request.UserAgent);
                ViewBag.ErrorMessage = "An unexpected error occurred during verification. Please try again later.";
                return View("Error");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> CheckVerificationStatus(string email)
        {
            try
            {
                // Log the email parameter
                _auditLogService.LogAudit(0, "Check Verification Attempt", "System", null, new { Email = email }, Request.UserHostAddress, Request.UserAgent);

                if (string.IsNullOrEmpty(email))
                {
                    _auditLogService.LogAudit(0, "Check Verification Failure", "User", null, new { Email = email, Error = "Email is required" }, Request.UserHostAddress, Request.UserAgent);
                    return Json(new { isVerified = false, error = "Email is required" }, JsonRequestBehavior.AllowGet);
                }

                var dbUser = _databaseService.ExecuteQuery<Models.User>("SELECT * FROM users WHERE email = @Email", new { Email = email }).FirstOrDefault();
                if (dbUser == null)
                {
                    _auditLogService.LogAudit(0, "Check Verification Failure", "User", null, new { Email = email, Error = "User not found" }, Request.UserHostAddress, Request.UserAgent);
                    return Json(new { isVerified = false, error = "User not found" }, JsonRequestBehavior.AllowGet);
                }

                var supabaseUser = await _authService.GetUserByIdAsync(dbUser.supabase_uid.ToString());
                if (supabaseUser?.EmailConfirmedAt != null && (bool)dbUser.is_active)
                {
                    _auditLogService.LogAudit(0, "Check Verification Success", "User", null, new { Email = email, SupabaseUid = dbUser.supabase_uid }, Request.UserHostAddress, Request.UserAgent);
                    return Json(new { isVerified = true }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { isVerified = false }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                _auditLogService.LogAudit(0, "Check Verification Error", "User", null, new { Email = email, Error = ex.Message }, Request.UserHostAddress, Request.UserAgent);
                return Json(new { isVerified = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> ResendConfirmation(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    ViewBag.ErrorMessage = "Email is required.";
                    return View("Verification");
                }

                var (success, message) = await _authService.ResendVerificationEmailAsync(email);

                if (success)
                {
                    ViewBag.Message = message;
                    ViewBag.Email = email;
                    TempData["Email"] = email;
                    TempData["SuccessMessage"] = message;
                }
                else
                {
                    ViewBag.ErrorMessage = message;
                    ViewBag.Email = email;
                }

                return View("Verification");
            }
            catch (Exception ex)
            {
                _auditLogService.LogAudit(0, "Resend Confirmation Error", "User", null, new { Email = email, Error = ex.Message }, Request.UserHostAddress, Request.UserAgent);
                ViewBag.ErrorMessage = "An unexpected error occurred. Please try again later.";
                ViewBag.Email = email;
                return View("Verification");
            }
        }
        #endregion

        #region Login
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            // Redirect if already authenticated
            ViewBag.ReturnUrl = returnUrl;
            if (returnUrl != null)
            {
                return RedirectToAction(returnUrl);
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                _auditLogService.LogAudit(0, "Login Validation Failure", "User", null, new { Email = model.Email, Error = "Invalid model state" }, Request.UserHostAddress, Request.UserAgent);
                return View(model);
            }

            try
            {
                var authResponse = await _authService.SignInWithEmailAsync(model.Email, model.Password);
                if (string.IsNullOrEmpty(authResponse.AccessToken))
                {
                    _auditLogService.LogAudit(0, "Login Failure", "User", null, new { Email = model.Email, Error = "Invalid email or password" }, Request.UserHostAddress, Request.UserAgent);
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(model);
                }
                // Replace the raw SQL query with Supabase ORM
                var dbUser = await _authService.UpdateAndGetUser(model.Email);
                if (dbUser == null || (bool)!dbUser.is_active)
                {
                    _auditLogService.LogAudit(0, "Login Failure", "User", null, new { Email = model.Email, Error = "User not found or inactive" }, Request.UserHostAddress, Request.UserAgent);
                    ModelState.AddModelError("", "Your account is not active or does not exist.");
                    return View(model);
                }

                // Use BaseController's SetCookie and SetUserPrincipal
                var setCookie = _authService.SignInUserAsync(dbUser, authResponse.AccessToken);
                if (setCookie == null) 
                {
                    _auditLogService.LogAudit(0, "Login Failure", "User", null, new { Email = model.Email, Error = "Failed to set authentication cookie" }, Request.UserHostAddress, Request.UserAgent);
                    ModelState.AddModelError("", "An error occurred while logging in. Please try again.");
                    return View(model);
                }
                _auditLogService.LogAudit(0, "Login Success", "User", null, new { Email = model.Email, SupabaseUid = dbUser.supabase_uid }, Request.UserHostAddress, Request.UserAgent);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _auditLogService.LogAudit(0, "Login Error", "User", null, new { Email = model.Email, Error = ex.Message }, Request.UserHostAddress, Request.UserAgent);
                ViewBag.ErrorMessage = "An unexpected error occurred during login. Please try again.";
                return View("Error");
            }
        }
        #endregion

        #region Forgot Password
        [HttpGet]
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _auditLogService.LogAudit(0, "Forgot Password Validation Failure", "User", null, new { Email = model.Email, Error = "Invalid model state" }, Request.UserHostAddress, Request.UserAgent);
                return View(model);
            }
            try
            {
                var (success, message) = await _authService.ResetPasswordForEmailAsync(model.Email);
                if (success)
                {
                    TempData["SuccessMessage"] = message;
                    return RedirectToAction("ForgotPasswordConfirmation");
                }
                else
                {
                    _auditLogService.LogAudit(0, "Forgot Password Failure", "User", null, new { Email = model.Email, Error = message }, Request.UserHostAddress, Request.UserAgent);
                    ViewBag.ErrorMessage = message ?? "Failed to send password reset email. Please try again.";
                    return View("Error");
                }
            }
            catch (Exception ex)
            {

                _auditLogService.LogAudit(0, "Forgot Password Error", "User", null, new { Email = model.Email, Error = ex.Message }, Request.UserHostAddress, Request.UserAgent);
                ViewBag.ErrorMessage = "An unexpected error occurred while processing your request. Please try again.";
                return View("Error");
            }
           
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            ViewBag.Message = TempData["SuccessMessage"] ?? "Please check your email for a password reset link.";
            return View();
        }
        #endregion

        #region Reset Password
        [HttpGet]
        [AllowAnonymous]
        public ActionResult ResetPassword()
        {
            return View(new ResetPasswordViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                var (success, message) = await _authService.UpdatePasswordAsync(model.AccessToken, model.RefreshToken, model.NewPassword);
                if (success)
                {
                    TempData["SuccessMessage"] = "Your password has been reset successfully. Please log in with your new password.";
                    return RedirectToAction("Login");
                }
                else
                {
                    TempData["ErrorMessage"] = message;
                    return View(model);
                }
            }
            catch (Exception ex) 
            {
                _auditLogService.LogAudit(0, "Reset Password Error", "User", null, new { Error = ex.Message }, Request.UserHostAddress, Request.UserAgent);
                ViewBag.ErrorMessage = "An unexpected error occurred while resetting your password. Please try again.";
                return View("Error");
            }
            
        }
        #endregion

        #region LogOff
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> LogOff(string returnUrl = null)
        {
            try
            {
                if (_authService == null)
                {
                    // Pass error message to Error.cshtml
                    ViewBag.ErrorMessage = "Authentication service is unavailable.";
                    return View("Error");
                }

                await _authService.SignOutAsync();

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                // Log the exception (e.g., using ILogger)
                ViewBag.ErrorMessage = "An error occurred during sign-out.";
                return View("Error");
            }
        }
        #endregion


        // Add these methods to your AccountController or create a ProfileController
        #region Profiles Methods

        [HttpGet]
        [Authorize]
        [ActionName("Profile")]

        public async Task<ActionResult> UserProfile()
        {
            try
            {
                // Get user ID from claims
                var claimsIdentity = User.Identity as ClaimsIdentity;
                var userIdClaim = claimsIdentity?.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _auditLogService.LogAudit(
                        userId: 0,
                        action: "Profile Access Failure",
                        entityType: "Profile",
                        entityId: null,
                        details: new { Error = "Invalid or missing UserId claim" },
                        ipAddress: GetClientIpAddress(),
                        userAgent: GetUserAgent()
                    );
                    ViewBag.ErrorMessage = "User session not found. Please try again after logging in.";
                    return View("Error");
                }

                // Query user from database with role information
                /*var query = @"
            SELECT u.user_id, u.email, u.first_name, u.last_name, u.phone_number,
                   u.is_active, u.created_at, u.updated_at, u.supabase_uid,
                   u.role_id, u.password_hash, u.profile_picture, u.last_login,
                   u.reset_token, u.reset_token_expires,
                   r.role_name
            FROM users u
            LEFT JOIN roles r ON u.role_id = r.role_id
            WHERE u.user_id = @UserId";*/

                var user = await _authService.GetUser(claimsIdentity.Name);

                if (user == null)
                {
                    _auditLogService.LogAudit(
                        userId: userId,
                        action: "Profile Access Failure",
                        entityType: "Profile",
                        entityId: userId,
                        details: new { Error = "User not found in database" },
                        ipAddress: GetClientIpAddress(),
                        userAgent: GetUserAgent()
                    );
                    ViewBag.ErrorMessage = "User profile not found. Please contact support.";
                    return View("Error");
                }

                // Create view model
                var viewModel = new ProfileViewModel
                {
                    UserId = user.user_id,
                    Email = user.email,
                    FirstName = user.first_name,
                    LastName = user.last_name,
                    PhoneNumber = user.phone_number,
                    IsActive = user.is_active,
                    RoleName = user.roles?.role_name ?? "User", // Handle null role
                    CreatedAt = user.created_at,
                    UpdatedAt = user.updated_at
                };

                // Log profile access
                _auditLogService.LogAudit(
                    userId: userId,
                    action: "Profile Accessed",
                    entityType: "Profile",
                    entityId: userId,
                    details: new { Action = "View Profile" },
                    ipAddress: GetClientIpAddress(),
                    userAgent: GetUserAgent()
                );

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log error
                var claimsIdentity = User.Identity as ClaimsIdentity;
                var userIdForLog = claimsIdentity?.FindFirst("UserId")?.Value ?? "Unknown";
                _auditLogService.LogAudit(
                    userId: 0,
                    action: "Profile Access Error",
                    entityType: "Profile",
                    entityId: null,
                    details: new { Error = ex.Message, UserId = userIdForLog },
                    ipAddress: GetClientIpAddress(),
                    userAgent: GetUserAgent()
                );

                ViewBag.ErrorMessage = "An unexpected error occurred while loading your profile. Please try again later.";
                return View("Error");
            }
        }

        // Helper method to safely get client IP address
        private string GetClientIpAddress()
        {
            try
            {
                // For ASP.NET Framework - try UserHostAddress first
                if (!string.IsNullOrEmpty(Request?.UserHostAddress))
                {
                    return Request.UserHostAddress;
                }

                // Check for forwarded headers (common in load balancer scenarios)
                if (Request?.Headers != null)
                {
                    string forwarded = Request.Headers["X-Forwarded-For"];
                    if (!string.IsNullOrEmpty(forwarded))
                    {
                        return forwarded.Split(',')[0].Trim();
                    }

                    forwarded = Request.Headers["X-Real-IP"];
                    if (!string.IsNullOrEmpty(forwarded))
                    {
                        return forwarded;
                    }
                }

                // Try server variables as fallback
                if (Request?.ServerVariables != null)
                {
                    string remoteAddr = Request.ServerVariables["REMOTE_ADDR"];
                    if (!string.IsNullOrEmpty(remoteAddr))
                    {
                        return remoteAddr;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore exceptions in IP detection
            }

            return "Unknown";
        }

        // Helper method to safely get User Agent
        private string GetUserAgent()
        {
            try
            {
                // For ASP.NET Framework - try UserAgent first
                if (!string.IsNullOrEmpty(Request?.UserAgent))
                {
                    return Request.UserAgent;
                }

                // Try headers collection
                if (Request?.Headers != null)
                {
                    string userAgent = Request.Headers["User-Agent"];
                    if (!string.IsNullOrEmpty(userAgent))
                    {
                        return userAgent;
                    }
                }

                // Try server variables as fallback
                if (Request?.ServerVariables != null)
                {
                    string userAgent = Request.ServerVariables["HTTP_USER_AGENT"];
                    if (!string.IsNullOrEmpty(userAgent))
                    {
                        return userAgent;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore exceptions in User Agent detection
            }

            return "Unknown";
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateProfile(string firstName, string lastName, string phoneNumber)
        {
            try
            {
                // Get user ID from claims (ASP.NET Framework approach)
                var claimsIdentity = User.Identity as ClaimsIdentity;
                var userIdClaim = claimsIdentity?.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Json(new { success = false, message = "User not found in session" });
                }

                // Validate input
                if (string.IsNullOrWhiteSpace(firstName))
                {
                    return Json(new { success = false, message = "First name is required" });
                }

                if (string.IsNullOrWhiteSpace(lastName))
                {
                    return Json(new { success = false, message = "Last name is required" });
                }

                // Update user profile
                var updateQuery = @"
            UPDATE users 
            SET first_name = @FirstName, 
                last_name = @LastName, 
                phone_number = @PhoneNumber, 
                updated_at = GETDATE() 
            WHERE user_id = @UserId";

                var result = _databaseService.ExecuteNonQuery(updateQuery, new
                {
                    FirstName = firstName.Trim(),
                    LastName = lastName.Trim(),
                    PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
                    UserId = userId
                });

                if (result > 0)
                {
                    // Log successful update
                    _auditLogService.LogAudit(
                        userId,
                        "Profile Updated",
                        "Profile",
                        userId,
                        new
                        {
                            Action = "Update Profile",
                            Changes = new { FirstName = firstName, LastName = lastName, PhoneNumber = phoneNumber }
                        },
                        Request.UserHostAddress ?? "Unknown",
                        Request.UserAgent ?? "Unknown"
                    );

                    return Json(new { success = true, message = "Profile updated successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "No changes were made to your profile" });
                }
            }
            catch (Exception ex)
            {
                // Log error
                var claimsIdentity = User.Identity as ClaimsIdentity;
                var userIdForLog = claimsIdentity?.FindFirst("UserId")?.Value ?? "Unknown";
                _auditLogService.LogAudit(
                    0,
                    "Profile Update Error",
                    "Profile",
                    null,
                    new { Error = ex.Message, UserId = userIdForLog },
                    Request.UserHostAddress ?? "Unknown",
                    Request.UserAgent ?? "Unknown"
                );

                return Json(new { success = false, message = "An error occurred while updating profile" });
            }
        }

        #endregion
    }
}
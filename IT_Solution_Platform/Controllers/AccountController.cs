using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Web.Services;
using IT_Solution_Platform.Models;
using IT_Solution_Platform.Services;
using Supabase.Gotrue;

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

                ViewBag.Email = model.Email;
                ViewBag.Message = message;
                TempData["Email"] = model.Email;
                TempData["SuccessMessage"] = message;



                // If access token is provided, user might be auto-verified
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var supabaseUser = await _authService.GetUserByTokenAsync(accessToken);
                    if (supabaseUser != null)
                    {
                        var dbUser = _databaseService.ExecuteQuery<Models.User>(
                            "SELECT * FROM users WHERE supabase_uid = CAST(@SupabaseUid AS uuid)",
                            new { SupabaseUid = supabaseUser.Id }).FirstOrDefault();

                        if (dbUser != null && (bool)dbUser.is_active)
                        {
                            return RedirectToAction("Index", "Home");
                        }
                    }
                }

                return RedirectToAction("Verify");
            }
            catch (Exception ex)
            {
                _auditLogService.LogAudit(0, "Registration Error", "User", null, new { Email = model.Email, Error = ex.Message }, Request.UserHostAddress, Request.UserAgent);
                ModelState.AddModelError("", "An unexpected error occurred during registration. Please try again later.");
                return View(model);
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

                    if ((bool)!dbUser.is_active)
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
                ViewBag.Message = "An error occurred during verification. Please try again or request a new confirmation email.";
                return View("Verification");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> CheckVerificationStatus(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
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
                var dbUser = _databaseService.ExecuteQuery<Models.User>("SELECT * FROM users WHERE email = @Email", new { Email = model.Email }).FirstOrDefault();
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
                ModelState.AddModelError("", "An error occurred during login. Please try again.");
                return View(model);
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

            var (success, message) = await _authService.ResetPasswordForEmailAsync(model.Email);
            if (success)
            {
                TempData["SuccessMessage"] = message;
                return RedirectToAction("ForgotPasswordConfirmation");
            }
            else
            {
                TempData["ErrorMessage"] = message;
                return View(model);
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
        #endregion

        #region LogOff
        [HttpGet]
        [AllowAnonymous]
        public ActionResult LogOff()
        {

            return RedirectToAction("Index", "Home");
        }
        #endregion
    }
}
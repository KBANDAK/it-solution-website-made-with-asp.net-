using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Web.Services.Description;
using IT_Solution_Platform.Models;
using IT_Solution_Platform.Services;
using Microsoft.Extensions.Logging;
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
        private readonly SupabaseAuthService _authService;
        private readonly SupabaseDatabase _database;
        private readonly AuditLogService _auditLogService;


        public AccountController(SupabaseAuthService authService, SupabaseDatabase database, AuditLogService auditLogService)
        {
            _authService = authService;
            _database = database;
            _auditLogService = auditLogService;
        }





        #region Registeration
        /// <summary>
        /// Displays the user registration view with pre-populated model data if needed.
        /// </summary>
        /// <returns>The registration view</returns>
        [HttpGet]
        [AllowAnonymous] // Explicitly allow unauthenticated access
        [OutputCache(Duration = 0, NoStore = true)] // Prevent caching for sensitive pages
        public ActionResult Register()
        {
            return View();
        }

        /// <summary>
        /// Register the user with the provided details. if successful sends an email to verify, then redirects to the verification page.
        /// </summary>
        /// <returns>The Verification Page</returns>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [OutputCache(Duration = 0, NoStore = true)] // Prevent caching for sensitive pages
        public async Task<ActionResult> Register(SignUpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _auditLogService.LogAudit(0, "Registration Attempt", "User", null, new { Email = model.Email, Error = "Invalid ModelState" }, Request.UserHostAddress, Request.UserAgent);
                return View(model);
            }

            try
            {
                // Check if user already exists in our database
                var existingUser = _database.ExecuteQuery<Models.User>("SELECT * FROM users WHERE email = @Email", new { Email = model.Email }).FirstOrDefault();
                if (existingUser != null)
                {
                    _auditLogService.LogAudit(0, "Registration Attempt", "User", null, new { Email = model.Email, Error = "User already exists" }, Request.UserHostAddress, Request.UserAgent);
                    ModelState.AddModelError("", "Registration Failed. Please try again later.");
                    return View(model);
                }

                // This will work inside a Controller
                var redirectUrl = Url.Action("Verify", "Account", null, Request.Url.Scheme);
                var options = new SignUpOptions
                {
                    RedirectTo = redirectUrl
                };

                // Register user with Supabase auth
                var (accessToken, message) = await _authService
                    .SignUpAsync(
                        model.Email,
                        model.Password,
                        model.FirstName,
                        model.LastName,
                        model.PhoneNumber,
                        options
                    );

               

                // Store email and message for verification page
                ViewBag.Email = model.Email;
                ViewBag.Message = message;


                return RedirectToAction("verify");
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

        /// <summary>
        /// Handles email verification for user registration. Processes Supabase confirmation links and displays the verification page.
        /// </summary>
        /// <param name="access_token">The Supabase access token from the confirmation link.</param>
        /// <param name="type">The type of verification (e.g., 'signup' for email confirmation).</param>
        /// <returns>The Verification view or a redirect to the dashboard after successful verification.</returns>
        [HttpGet]
        [AllowAnonymous]
        [ActionName("verify")]
        public async Task<ActionResult> VerificationAsync(string access_token = null, string type = null, string error = null, string error_code = null, string error_description = null)
        {
            try
            {

                // Handle error case from Supabase
                if (!string.IsNullOrEmpty(error))
                {
                    _auditLogService.LogAudit(0, "Verification Error", "User", null, new { Error = error, ErrorCode = error_code, ErrorDescription = error_description }, Request.UserHostAddress, Request.UserAgent);
                    ViewBag.Message = error_description?.Replace("+", " ") ?? "An error occurred during verification. Please request a new confirmation email.";
                    return View("Verification");
                }

                if (!string.IsNullOrEmpty(access_token) && type == "signup")
                {
                    // Verify the access token with Supabase
                    var supabaseUser = await _authService.GetUserByTokenAsync(access_token);

                    if (supabaseUser == null)
                    {
                        _auditLogService.LogAudit(0, "Verification Failure", "User", null, new { Error = "Invalid or expired access token" }, Request.UserHostAddress, Request.UserAgent);
                        ViewBag.Message = "The verification link is invalid or expired. Please request a new confirmation email.";
                        return View("Verification");
                    }

                    // Check if user exists in the database
                    var dbUser = _database.ExecuteQuery<Models.User>("SELECT * FROM users WHERE supabase_uid = CAST(@SupabaseUid AS uuid)", new { SupabaseUid = supabaseUser.Id }).FirstOrDefault();
                    if (dbUser == null)
                    {
                        _auditLogService.LogAudit(0, "Verification Failure", "User", null, new { Email = supabaseUser.Email, Error = "User not found in database" }, Request.UserHostAddress, Request.UserAgent);
                        ViewBag.Message = "User not found. Please register again.";
                        return View("Verification");
                    }

                    // Update user as active in the database
                    if ((bool)!dbUser.IsActive)
                    {
                        var updateQuery = "UPDATE users SET is_active = @IsActive, updated_at = @UpdatedAt WHERE supabase_uid = CAST(@SupabaseUid AS uuid)";
                        var updateResult = _database.ExecuteNonQuery(updateQuery, new
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
                    }
                    // Store token securely in cookie
                    var cookie = new HttpCookie("access_token", access_token)
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.UtcNow.AddHours(1)
                    };

                    Session.Add("Fullname", dbUser.FullName);
                    Response.Cookies.Add(cookie);

                    // Log successful verification
                    _auditLogService.LogAudit(0, "Verification Success", "User", null, new { Email = supabaseUser.Email, SupabaseUid = supabaseUser.Id }, Request.UserHostAddress, Request.UserAgent);


                    // Redirect to home or dashboard
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

        /// <summary>
        /// Checks if the user’s email has been verified.
        /// </summary>
        /// <param name="email">The user’s email address.</param>
        /// <returns>JSON indicating verification status.</returns>
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

                // Check database for user status
                var dbUser = _database.ExecuteQuery<Models.User>("SELECT * FROM users WHERE email = @Email", new { Email = email }).FirstOrDefault();
                if (dbUser == null)
                {
                    _auditLogService.LogAudit(0, "Check Verification Failure", "User", null, new { Email = email, Error = "User not found" }, Request.UserHostAddress, Request.UserAgent);
                    return Json(new { isVerified = false, error = "User not found" }, JsonRequestBehavior.AllowGet);
                }

                // Optionally, verify with Supabase
                var supabaseUser = await _authService.GetUserByIdAsync("1");
                if (supabaseUser?.EmailConfirmedAt != null)
                {
                    // Update database if not already active
                    if ((bool)!dbUser.IsActive)
                    {
                        var updateQuery = "UPDATE users SET is_active = @IsActive, updated_at = @UpdatedAt WHERE supabase_uid = @SupabaseUid";
                        var updateResult = _database.ExecuteNonQuery(updateQuery, new
                        {
                            IsActive = true,
                            UpdatedAt = DateTime.UtcNow,
                            SupabaseUid = supabaseUser.Id
                        });

                        if (updateResult <= 0)
                        {
                            _auditLogService.LogAudit(0, "Check Verification Failure", "User", null, new { Email = email, Error = "Failed to update user status" }, Request.UserHostAddress, Request.UserAgent);
                            return Json(new { isVerified = false, error = "Failed to update user status" }, JsonRequestBehavior.AllowGet);
                        }
                    }

                    _auditLogService.LogAudit(0, "Check Verification Success", "User", null, new { Email = email, SupabaseUid = supabaseUser.Id }, Request.UserHostAddress, Request.UserAgent);
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
        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
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
                // Authenticate with supabase
                var authResponse = await _authService.SignInWithEmailAsync(model.Email, model.Password);

                if (authResponse.Message == null || string.IsNullOrEmpty(authResponse.AccessToken))
                {
                    _auditLogService.LogAudit(0, "Login Failure", "User", null, new { Email = model.Email, Error = "Invalid email or password" }, Request.UserHostAddress, Request.UserAgent);
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(model);
                }

                // Fetch user details from the database
                var dbUser = _database.ExecuteQuery<Models.User>("SELECT * FROM users WHERE email = @Email", new { Email = model.Email }).FirstOrDefault();
                if (dbUser == null || (bool)!dbUser.IsActive)
                {
                    _auditLogService.LogAudit(0, "Login Failure", "User", null, new { Email = model.Email, Error = "User not found or inactive" }, Request.UserHostAddress, Request.UserAgent);
                    ModelState.AddModelError("", "Your account is not active or does not exist.");
                    return View(model);
                }

                // Set access_token cookie
                var cookie = new HttpCookie("access_token", authResponse.AccessToken)
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = model.RememberMe ? DateTime.UtcNow.AddDays(7) : DateTime.UtcNow.AddHours(1)
                };
                Response.Cookies.Add(cookie);

                // Set Fullname in session
                Session["Fullname"] = dbUser.FirstName ?? model.Email ?? "User";

                // Log successful login
                _auditLogService.LogAudit(0, "Login Success", "User", null, new { Email = model.Email, SupabaseUid = dbUser.SupabaseUid }, Request.UserHostAddress, Request.UserAgent);

                // Redirect to returnUrl or home
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

        // Get : / Account/ForgotPassword
        [HttpGet]
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // Post: /Account/ForgotPassword
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

        // Get: /Account/ForgotPasswordConfirmation
        [HttpGet]
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            ViewBag.Message = TempData["SuccessMessage"] ?? "Please check your email for a password reset link.";
            return View();
        }
        #endregion

        #region Reset Password
        // Get: /Account/ResetPassword
        [HttpGet]
        [AllowAnonymous]
        public ActionResult ResetPassword() 
        {
            var model = new ResetPasswordViewModel();
            return View(model);
        }

        // Post: /Account/ResetPassword
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
        [Authorize]
        public ActionResult LogOff() 
        {
            // Clear the access_token cookie
            var cookie = new HttpCookie("access_token")
            {
                Expires = DateTime.UtcNow.AddDays(-1),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            };
            Response.Cookies.Add(cookie);

            // Clear session
            Session.Clear();
            Session.Abandon();


            // Clear authentication
            System.Web.Security.FormsAuthentication.SignOut(); // If usign forms authentication
            HttpContext.User = null;
            System.Threading.Thread.CurrentPrincipal = null;

            return RedirectToAction("Index", "Home");
        }

        #endregion
    }
}

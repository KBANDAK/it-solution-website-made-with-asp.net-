using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Microsoft.Owin; // Add this
using System.Windows.Forms.VisualStyles;
using IT_Solution_Platform.Models;
using Newtonsoft.Json;
using Supabase.Gotrue;
using static System.Net.WebRequestMethods;
using Microsoft.Owin.Security;

namespace IT_Solution_Platform.Services
{
    /// <summary>
    /// Handles authentication operations with Supabase, including user sign-up, sign-in, and email confirmation.
    /// Integrates with the users table and logs suspicious activities.
    /// </summary>
    public class SupabaseAuthService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly SupabaseDatabase _database;
        private readonly AuditLogService _auditLog;
        private const string PENDING_USER_SESSION_KEY = "PendingUser";

        /// <summary>
        /// Initializes a new instance of the SupabaseAuthService.
        /// </summary>
        public SupabaseAuthService()
        {
            _supabaseClient = SupabaseConfig.GetAnonClient(); // Use the anonymous client for user sign-up and sign-in
            _database = new SupabaseDatabase();
            _auditLog = new AuditLogService(_database);
        }

        public async Task<(string AccessToken, string Message)> SignUpAsync(string email, string password, string firstName, string lastName, string phoneNumber, SignUpOptions options)
        {
            try
            {
                // Validate input parameters
                if (string.IsNullOrWhiteSpace(email))
                    return (null, "Email is required.");

                if (string.IsNullOrWhiteSpace(password))
                    return (null, "Password is required.");

                if (string.IsNullOrWhiteSpace(firstName))
                    return (null, "First name is required.");

                if (string.IsNullOrWhiteSpace(lastName))
                    return (null, "Last name is required.");



                var session = await _supabaseClient.Auth.SignUp(email, password, options);


                if (session?.User == null)
                {
                    _auditLog.LogAudit(0, "Registration failed", lastName, null, new { Email = email }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                    return (null, "Registration failed. Please check your email and password requirements.");
                }

                // If we get here, the user was created successfully
                var accessToken = session.AccessToken;

                /*if (string.IsNullOrEmpty(accessToken))
                {
                    _auditLog.LogAudit(0, "Registration failed", lastName, null, new { Email = email }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                    return (null, "Registration completed but authentication failed. Please try signing in.");
                }*/

                // Get default role (e.g., role_id = 1 for normal users
                var roles = _database.ExecuteQuery<Role>("SELECT * FROM roles where role_id = @role_id", new { role_id = 1 });
                var defaultRole = roles.FirstOrDefault();
                if (defaultRole == null)
                {
                    _auditLog.LogAudit(0, "Registration Failure", "User", null, new { Email = email, Error = "Default role not found" }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                    return (null, "Registration failed. Default role not found.");
                }

                // Insert user metadata into the database
                var supabaseUid = Guid.TryParse(session.User.Id, out var uid) ? uid : throw new Exception("Invalid Supabase UID");
                var user = new Models.User
                {
                    email = email,
                    first_name = firstName,
                    last_name = lastName,
                    phone_number = phoneNumber,
                    is_active = accessToken != null, // Active only if no email confirmation is required
                    supabase_uid = supabaseUid,
                    role_id = defaultRole.role_id,
                    password_hash = "supabase_managed",
                };

                var insertQuery = @"INSERT INTO users (email, first_name, last_name, phone_number, is_active, supabase_uid, role_id, password_hash)
                           VALUES (@Email, @FirstName, @LastName, @PhoneNumber, @IsActive, @SupabaseUid, @RoleId, @PasswordHash)";

                var insertResult = _database.ExecuteNonQuery(insertQuery, user);
                if (insertResult <= 0)
                {
                    _auditLog.LogAudit(0, "Registration failed", lastName, null, new { Email = email, Error = "Insertion in users table error." }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);

                    // Clean up supabase base user if database insertion fails
                    try
                    {

                        await _supabaseClient.AdminAuth(SupabaseConfig.SupabaseServiceKey).DeleteUser(session.User.Id);
                    }
                    catch (Exception cleanUpEx)
                    {
                        _auditLog.LogAudit(0, "Registration failed", lastName, null, new { Email = email, Error = cleanUpEx.Message }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                    }

                    return (null, "Registration failed. Please try again later.");
                }

                return (accessToken, "Registration successful!");
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException gotrueEx)
            {
                // Handle Supabase-specific authentication errors
                _auditLog.LogAudit(0, "Registration failed", lastName, null, new { Email = email, Exception = gotrueEx.Message }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                return (null, $"Registration failed: {gotrueEx.Message}");

            }
            catch (HttpException httpEx)
            {
                // Handle HTTP-related errors
                if (httpEx.GetHttpCode() == 429) // Rate limiting
                {
                    _auditLog.LogAudit(0, "Registration failed, Too many requests", lastName, null, new { Email = email, Exception = httpEx.Message }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                    return (null, "Too many requests. Please wait a moment and try again.");
                }

                _auditLog.LogAudit(0, "Registration failed, Network Error.", lastName, null, new { Email = email }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                return (null, "Network error occurred during registration. Please check your connection and try again.");
            }
            catch (Exception ex)
            {
                // Handle any other unexpected errors
                _auditLog.LogAudit(0, "Registration failed, unexpected error.", lastName, null, new { Email = email, Exception = ex }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                return (null, "An unexpected error occurred during registration. Please try again later.");
            }
        }

        public async Task<(string AccessToken, string Message)> SignInWithEmailAsync(string email, string password)
        {
            try
            {
                var session = await _supabaseClient.Auth.SignInWithPassword(email, password);
                if (session != null && !string.IsNullOrEmpty(session.AccessToken))
                {
                    return (session.AccessToken, "Login successful");
                }

                return (null, "Invalid email or password");
            }
            catch (Exception ex)
            {
                return (null, $"Authentication failed: {ex.Message}");
            }
        }

        public Task<(bool Success, string Message)> ConfirmEmailAsync(string userId, string token)
        {
            throw new NotImplementedException();
        }

        public Task<(bool Success, string Message)> ResendVerificationEmailAsync(string email)
        {
            throw new NotImplementedException();
        }

        public async Task<(bool Success, string Message)> ResetPasswordForEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    _auditLog.LogAudit(0, "Password Reset Failed", "User", null, new { Email = email, Error = "Email is required" }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");
                    return (false, "Email is required.");
                }

                // Create the redirect URL properly
                var request = HttpContext.Current.Request;
                var redirectUrl = $"{request.Url.Scheme}://{request.Url.Authority}/Account/ResetPassword";

                // Log the redirect URL for debugging
                _auditLog.LogAudit(0, "Password Reset Redirect URL", "System", null, new { RedirectUrl = redirectUrl }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");

                // Trigger Supabase password reset email
                await _supabaseClient.Auth.ResetPasswordForEmail(new ResetPasswordForEmailOptions(email)
                {
                    RedirectTo = redirectUrl
                });

                _auditLog.LogAudit(0, "Password Reset Email Sent", "User", null, new { Email = email }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");
                return (true, "If this email is registered, a password reset link has been sent.");
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException gotrueEx)
            {
                _auditLog.LogAudit(0, "Password Reset Failed", "User", null, new { Email = email, Exception = gotrueEx.Message }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");

                // Always return success message to prevent information disclosure
                // This prevents attackers from knowing if an email exists or rate limits
                return (true, "If this email is registered, a password reset link has been sent.");
            }
            catch (Exception ex)
            {
                _auditLog.LogAudit(0, "Password Reset Failed", "User", null, new { Email = email, Exception = ex.Message }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");

                // Always return success message for security
                return (true, "If this email is registered, a password reset link has been sent.");
            }
        }

        public async Task<(bool Success, string Message)> UpdatePasswordAsync(string accessToken, string refreshToken, string newPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(newPassword))
                {
                    _auditLog.LogAudit(0, "Password Update Failed", "User", null, new { Error = "Access token and password are required" }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");
                    return (false, "Invalid request parameters.");
                }

                // Set the session with both tokens
                await _supabaseClient.Auth.SetSession(accessToken, refreshToken);

                // Update the password
                var response = await _supabaseClient.Auth.Update(new UserAttributes
                {
                    Password = newPassword
                });

                if (response != null)
                {
                    _auditLog.LogAudit(0, "Password Updated Successfully", "User", Int32.Parse(response.Id), new { }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");
                    return (true, "Password updated successfully.");
                }
                return (false, "Failed to update password.");
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException gotrueEx)
            {
                _auditLog.LogAudit(0, "Password Update Failed", "User", null, new { Exception = gotrueEx.Message }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");

                // Parse the error message to provide user-friendly feedback
                string userFriendlyMessage = ParseGoTrueError(gotrueEx.Message);
                return (false, userFriendlyMessage);
            }
            catch (Exception ex)
            {
                _auditLog.LogAudit(0, "Password Update Failed", "User", null, new { Exception = ex.Message }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");
                return (false, "An unexpected error occurred. Please try again later.");
            }
        }


        public async Task<Supabase.Gotrue.User> GetUserByTokenAsync(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new ArgumentException("Access token cannot be null or empty.");
                }

                // Use supabase auth to get the user by token 
                var user = await _supabaseClient.Auth.GetUser(accessToken);
                if (user == null)
                {
                    _auditLog.LogAudit(0, "GetUserByToken Failure", "User", null, new { Error = "Invalid or expired token" }, "Unknown", "Unknown");
                    return null;
                }
                return user;
            }
            catch (Exception ex)
            {
                _auditLog.LogAudit(0, "GetUserByToken Failure", "User", null, new { Error = ex.Message }, "Unknown", "Unknown");
                return null;
            }
        }

        public async Task<Supabase.Gotrue.User> GetUserByIdAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("User ID cannot be null or empty.");
                }

                var adminClient = SupabaseConfig.GetServiceClient();
                var user = await adminClient.AdminAuth(SupabaseConfig.SupabaseServiceKey).GetUserById(userId);
                if (user == null)
                {
                    _auditLog.LogAudit(0, "GetUserById Failure", "User", null, new { Error = "User not found" }, "Unknown", "Unknown");
                    return null;
                }
                return user;
            }
            catch (Exception ex)
            {
                _auditLog.LogAudit(0, "GetUserById Failure", "User", null, new { UserId = userId, Error = ex.Message }, "Unknown", "Unknown");
                return null;
            }
        }

        public async Task<Session> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var response = await _supabaseClient.Auth.RefreshSession();
                return response;
            }
            catch (Exception ex)
            {
                _auditLog.LogAudit(0, "Token Refresh Failed", "User", null, new { Error = ex.Message }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");
                throw new ApplicationException($"Token refresh failed: {ex.Message}", ex);
            }
        }


        // Sign In with user async - CORRECTED VERSION
        public Task SignInUserAsync(Models.User user, string accessToken)
        {
            try
            {
                // Create claims for a user
                var claims = new[]
                {
                     new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                     new Claim(ClaimTypes.Name, user.email),
                     new Claim(ClaimTypes.Email, user.email),
                     new Claim("SupabaseUserId", user.supabase_uid.ToString()),
                     new Claim("UserId", user.user_id.ToString()),
                     new Claim("FirstName", user.first_name ?? ""),
                     new Claim("LastName", user.last_name ?? ""),
                     new Claim("AccessToken", accessToken ?? ""),
                     new Claim("RoleId", user.role_id.ToString()),
                     new Claim(ClaimTypes.Role, user.Role?.role_name ?? "") // Optional: Add role name
                };

                var identity = new ClaimsIdentity(claims, "ApplicationCookie");
                var principal = new ClaimsPrincipal(identity);

                // Sign in with OWIN
                var authManager = HttpContext.Current.GetOwinContext().Authentication;
                authManager.SignIn(new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                }, identity);

                // Also set FormsAuthentication cookie for compatibility
                FormsAuthentication.SetAuthCookie(user.email, false);

                // Explicitly set HttpContext.User to ensure claims are available
                HttpContext.Current.User = principal;

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _auditLog.LogAudit(user.user_id, "SignIn Failed", "Authentication", null,
                    new { Error = ex.Message },
                    HttpContext.Current?.Request.UserHostAddress ?? "Unknown",
                    HttpContext.Current?.Request.UserAgent ?? "Unknown");
                throw;
            }
        }


        // Sign out - CORRECTED VERSION
        public async Task SignOutAsync()
        {
            try
            {
                // Sign out from Supabase
                await _supabaseClient.Auth.SignOut();

                // Sign out from OWIN
                var authManager = HttpContext.Current.GetOwinContext().Authentication;
                authManager.SignOut("ApplicationCookie");

                // Clear Forms Authentication
                FormsAuthentication.SignOut();

                // Clear session
                HttpContext.Current.Session.Clear();
            }
            catch (Exception ex)
            {
                // Even if Supabase signout fails, clear local auth
                try
                {
                    var authManager = HttpContext.Current.GetOwinContext().Authentication;
                    authManager.SignOut("ApplicationCookie");
                    FormsAuthentication.SignOut();
                    HttpContext.Current.Session.Clear();
                }
                catch
                {
                    // Fallback - just clear session and forms auth
                    FormsAuthentication.SignOut();
                    HttpContext.Current.Session.Clear();
                }

                throw new Exception($"Sign out failed: {ex.Message}", ex);
            }
        }

        private string ParseGoTrueError(string errorMessage)
        {
            try
            {
                // Parse the JSON error message
                var errorJson = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(errorMessage);
                string errorCode = errorJson?.error_code?.ToString() ?? "";
                string msg = errorJson?.msg?.ToString() ?? "";

                // Handle specific error codes with user-friendly messages
                switch (errorCode.ToLower())
                {
                    case "same_password":
                        return "The new password must be different from your current password.";

                    case "weak_password":
                        return "The password is too weak. Please choose a stronger password with at least 8 characters, including uppercase, lowercase, numbers, and special characters.";

                    case "password_too_short":
                        return "The password is too short. Please choose a password with at least 8 characters.";

                    case "invalid_credentials":
                        return "Your session has expired. Please request a new password reset link.";

                    case "token_expired":
                        return "The password reset link has expired. Please request a new password reset link.";

                    case "invalid_token":
                        return "The password reset link is invalid. Please request a new password reset link.";

                    case "user_not_found":
                        return "User account not found. Please contact support if this error persists.";

                    case "signup_disabled":
                        return "Password reset is currently disabled. Please contact support.";

                    default:
                        // If we have a user-friendly message from Supabase, use it
                        if (!string.IsNullOrEmpty(msg) && !msg.Contains("JSON") && !msg.Contains("{"))
                        {
                            return msg;
                        }
                        // Otherwise return a generic message
                        return "Password update failed. Please try again or contact support if the problem persists.";
                }
            }
            catch
            {
                // If JSON parsing fails, try to extract meaningful information
                if (errorMessage.Contains("same_password"))
                    return "The new password must be different from your current password.";

                if (errorMessage.Contains("weak_password"))
                    return "The password is too weak. Please choose a stronger password.";

                if (errorMessage.Contains("password_too_short"))
                    return "The password is too short. Please choose a longer password.";

                if (errorMessage.Contains("invalid_credentials") || errorMessage.Contains("token_expired"))
                    return "Your session has expired. Please request a new password reset link.";

                if (errorMessage.Contains("invalid_token"))
                    return "The password reset link is invalid. Please request a new password reset link.";

                // Default fallback
                return "Password update failed. Please try again or contact support if the problem persists.";
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Windows.Forms.VisualStyles;
using IT_Solution_Platform.Models;
using Microsoft.AspNet.Identity;
using Microsoft.Owin; // Add this
using Microsoft.Owin.Security;
using Newtonsoft.Json;
using Supabase.Gotrue;
using static System.Net.WebRequestMethods;

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
                var roles = _database.ExecuteQuery<Role>("SELECT * FROM roles where role_id = @role_id", new { role_id = 1 }); // User
                var defaultRole = roles.FirstOrDefault();
                if (defaultRole == null)
                {
                    _auditLog.LogAudit(0, "Registration Failure", "User", null, new { Email = email, Error = "Default role not found" }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                    return (null, "Registration failed. Default role not found.");
                }

                // Insert user metadata into the database
                var supabaseUid = Guid.TryParse(session.User.Id, out var uid) ? uid : throw new Exception("Invalid Supabase UID");
                var userParameters = new
                {
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    PhoneNumber = phoneNumber,
                    IsActive = false, // Active only if no email confirmation is required
                    SupabaseUid = supabaseUid,
                    RoleId = defaultRole.role_id,
                    PasswordHash = "supabase_managed",
                };

                var insertQuery = @"INSERT INTO users (email, first_name, last_name, phone_number, is_active, supabase_uid, role_id, password_hash)
                           VALUES (@Email, @FirstName, @LastName, @PhoneNumber, @IsActive, @SupabaseUid, @RoleId, @PasswordHash)";

                var insertResult = _database.ExecuteNonQuery(insertQuery, userParameters);
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


        public async Task<Models.User> UpdateAndGetUser(string email)
        {
            try
            {
                // Use database function to avoid RLS recursion issues
                var result = await _supabaseClient.Postgrest.Rpc("update_user_last_login", new Dictionary<string, object>
                {
                    { "user_email", email }
                });

                if (result?.Content != null)
                {
                    // Parse the result to get the updated user
                    var updatedUser = System.Text.Json.JsonSerializer.Deserialize<Models.User>(result.Content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return updatedUser;
                }

                // Fallback: get user without updating if function fails
                var dbUser = await _supabaseClient
                    .From<Models.User>()
                    .Where(u => u.email == email)
                    .Single();

                return dbUser;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update user login timestamp: {ex.Message}", ex);
            }
        }

        public async Task<Models.User> GetUser(string email)
        {
            try
            {
                var result = await _supabaseClient
                    .From<Models.User>()
                    .Select("*, roles(*)")
                    .Where(u => u.email == email)
                    .Single();

                // Extract user and role from the joined result
                var dbUser = result;


                return dbUser;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get user : {ex.Message}", ex);
            }
        }


        public async Task<(bool success, string message)> UpdateUser(string userSId, string firstName , string lastName, string phoneNumber)
        {
            try
            {
                // Validate user object
                if ( string.IsNullOrEmpty(userSId) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(phoneNumber))
                {
                    return (false, $"Email , Firstname, Lastname or Phonenumber is null {userSId} ... {firstName} ... , {lastName} ... ,{phoneNumber}");
                    
                }

                // Make sure you're authenticated
                var currentUser = _supabaseClient.Auth.CurrentUser;
                if (currentUser == null)
                {
                    return (false, "User not authenticated");
                }

                Guid uid = Guid.Parse(userSId);


                // Method 1: Update using Set method (Recommended)
                var result = await _supabaseClient
                    .From<Models.User>()
                    .Where(u => u.supabase_uid == uid)
                    .Set(u => u.first_name, firstName)
                    .Set(u => u.last_name, lastName)
                    .Set(u => u.phone_number, phoneNumber)
                    .Update();


                if (result == null)
                {
                    return (false, $"Update user failed with updated is (Possiable null): {result}");
                }
                return (result.ResponseMessage.IsSuccessStatusCode, "User updated successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Update user failed with ex: {ex}");
            }
        }
        public Task<(bool Success, string Message)> ConfirmEmailAsync(string userId, string token)
        {
            throw new NotImplementedException();
        }

        public async Task<(bool Success, string Message)> ResendVerificationEmailAsync(string email)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(email))
                {
                    return (false, "Email is required.");
                }

                // Check if user exists in database
                var dbUser = _database.ExecuteQuery<Models.User>("SELECT * FROM users WHERE email = @Email", new { Email = email }).FirstOrDefault();
                if (dbUser == null)
                {
                    _auditLog.LogAudit(0, "Resend Verification Failed", "User", null, new { Email = email, Error = "User not found" }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                    return (false, "User not found. Please register first.");
                }

                // Check if user is already verified
                if (dbUser.is_active)
                {
                    _auditLog.LogAudit(0, "Resend Verification Skipped", "User", null, new { Email = email, Reason = "Already verified" }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                    return (true, "Your email is already verified. You can sign in now.");
                }

                // Get user from Supabase to check verification status
                var supabaseUser = await _supabaseClient.AdminAuth(SupabaseConfig.SupabaseServiceKey).GetUserById(dbUser.supabase_uid.ToString());
                if (supabaseUser == null)
                {
                    _auditLog.LogAudit(0, "Resend Verification Failed", "User", null, new { Email = email, Error = "Supabase user not found" }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                    return (false, "User account not found. Please register again.");
                }

                // Check if email is already confirmed in Supabase
                if (supabaseUser.EmailConfirmedAt.HasValue)
                {
                    // Update local database if Supabase shows verified but local doesn't
                    var updateQuery = "UPDATE users SET is_active = @IsActive, updated_at = @UpdatedAt WHERE supabase_uid = @SupabaseUid";
                    _database.ExecuteNonQuery(updateQuery, new
                    {
                        IsActive = true,
                        UpdatedAt = DateTime.UtcNow,
                        SupabaseUid = dbUser.supabase_uid
                    });

                    return (true, "Your email is already verified. You can sign in now.");
                }

                // Resend verification email through Supabase
                var redirectUrl = HttpContext.Current.Request.Url.Scheme + "://" + HttpContext.Current.Request.Url.Authority + "/Account/verify";

                await _supabaseClient.AdminAuth(SupabaseConfig.SupabaseServiceKey).InviteUserByEmail(email, new InviteUserByEmailOptions
                {
                    RedirectTo = redirectUrl
                });

                _auditLog.LogAudit(0, "Resend Verification Success", "User", null, new { Email = email }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                return (true, "Verification email has been resent. Please check your inbox and spam folder.");
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException gotrueEx)
            {
                _auditLog.LogAudit(0, "Resend Verification Failed", "User", null, new { Email = email, Error = gotrueEx.Message }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);

                // Handle specific Supabase errors
                if (gotrueEx.Message.Contains("rate limit"))
                {
                    return (false, "Too many requests. Please wait a few minutes before requesting another verification email.");
                }

                return (false, $"Failed to resend verification email: {gotrueEx.Message}");
            }
            catch (Exception ex)
            {
                _auditLog.LogAudit(0, "Resend Verification Error", "User", null, new { Email = email, Error = ex.Message }, HttpContext.Current.Request.UserHostAddress, HttpContext.Current.Request.UserAgent);
                return (false, "An unexpected error occurred. Please try again later.");
            }
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
                    var user = await GetUser(response.Email);
                    if (user == null) 
                    {
                        _auditLog.LogAudit(0, "Password Updated Successfully", "User", 0, new { }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");
                    }
                    _auditLog.LogAudit(0, "Password Updated Successfully", "User", user.user_id, new { }, HttpContext.Current?.Request.UserHostAddress ?? "Unknown", HttpContext.Current?.Request.UserAgent ?? "Unknown");
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

        public async Task<(bool Success,  string Message)> ResetUserPasswordAsync(string subabaseUid, string password)
        {
            try
            {
                

                // Update user password using Supabase Auth Admin API
                // Note: This requires proper Supabase setup with service role key
                var authResponse = await _supabaseClient.AdminAuth(SupabaseConfig.SupabaseServiceKey).UpdateUserById(
                    subabaseUid,
                    new Supabase.Gotrue.AdminUserAttributes
                    {
                        Password = password
                    }
                );

                if (authResponse != null)
                {
                    string message = "Password Reset Sucessfully!";
                    return (true, message);
                }

                return (false, "Failed to reset password");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting password: {ex.Message}");
                return (false, $"Error: {ex.Message}");
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
                     new Claim(ClaimTypes.Role, user.roles?.role_name ?? "") // Optional: Add role name
                };

                var identity = new ClaimsIdentity(claims, "ApplicationCookie");
                var principal = new ClaimsPrincipal(identity);

                // Sign in with OWIN
                var authManager = HttpContext.Current.GetOwinContext().Authentication;
                authManager.SignIn(new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
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
                if (_supabaseClient.Auth != null) 
                {
                    await _supabaseClient.Auth.SignOut();
                }

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
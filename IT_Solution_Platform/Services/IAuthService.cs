using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using IT_Solution_Platform.Models;
using Supabase.Gotrue;

namespace IT_Solution_Platform.Services
{
    /// <summary>
    /// Interface for authentication services
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new user
        /// </summary>
        Task<(string AccessToken, string Message)> SignUpAsync(string email, string password, string firstName, string lastName, string phoneNumber, SignUpOptions options);

        /// <summary>
        /// Authenticates a user with email and password
        /// </summary>
        Task<(string AccessToken, string Message)> SignInWithEmailAsync(string email, string password);

        /// <summary>
        /// Confirms a user's email address
        /// </summary>
        Task<(bool Success, string Message)> ConfirmEmailAsync(string userId, string token);

        /// <summary>
        /// Resends verification email
        /// </summary>
        Task<(bool Success, string Message)> ResendVerificationEmailAsync(string email);

        /// <summary>
        /// Initiates password reset process
        /// </summary>
        Task<(bool Success, string Message)> ResetPasswordForEmailAsync(string email);

        /// <summary>
        /// Updates user's password
        /// </summary>
        Task<(bool Success, string Message)> UpdatePasswordAsync(string accessToken, string refershToken, string newPassword);

        /// <summary>
        /// Gets user by access token
        /// </summary>
        Task<Supabase.Gotrue.User> GetUserByTokenAsync(string accessToken);

        /// <summary>
        /// Gets user by ID
        /// </summary>
        Task<Supabase.Gotrue.User> GetUserByIdAsync(string userId);

        /// <summary>
        /// Signs out user
        /// </summary>
        Task<bool> SignOutAsync();

        /// <summary>
        /// Refreshes access token
        /// </summary>
        Task<(string AccessToken, string Message)> RefreshTokenAsync(string refreshToken);
    }
}
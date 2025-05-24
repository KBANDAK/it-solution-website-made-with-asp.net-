using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT_Solution_Platform.Models
{
    /// <summary>
    /// Represents a user entity in the database, mapping to the public.users table.
    /// </summary>
    public class User
    {
        /// <summary>
        /// The unique identifier for the user (auto-incremented).
        /// </summary>
        [Key]
        public int UserId { get; set; }

        /// <summary>
        /// The role ID associated with the user (foreign key to roles table).
        /// </summary>
        public int? RoleId { get; set; }

        /// <summary>
        /// The user's email address (unique).
        /// </summary>
        [Required]
        [StringLength(255)]
        [EmailAddress]
        public string Email { get; set; }

        /// <summary>
        /// The hashed password (managed by Supabase in this implementation).
        /// </summary>
        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; }

        /// <summary>
        /// The user's first name.
        /// </summary>
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        /// <summary>
        /// The user's last name.
        /// </summary>
        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        /// <summary>
        /// The user's phone number (optional).
        /// </summary>
        [StringLength(20)]
        [Phone]
        public string PhoneNumber { get; set; }

        /// <summary>
        /// The URL or path to the user's profile picture (optional).
        /// </summary>
        [StringLength(255)]
        public string ProfilePicture { get; set; }

        /// <summary>
        /// Indicates whether the user account is active.
        /// </summary>
        public bool? IsActive { get; set; } = true;

        /// <summary>
        /// The timestamp of the user's last login.
        /// </summary>
        public DateTime? LastLogin { get; set; }

        /// <summary>
        /// The token used for password reset (optional).
        /// </summary>
        [StringLength(255)]
        public string ResetToken { get; set; }

        /// <summary>
        /// The expiration timestamp for the password reset token.
        /// </summary>
        public DateTime? ResetTokenExpires { get; set; }

        /// <summary>
        /// The timestamp when the user was created.
        /// </summary>
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The timestamp when the user was last updated.
        /// </summary>
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The Supabase user ID (unique, links to Supabase auth).
        /// </summary>
        [StringLength(255)]
        public Guid SupabaseUid { get; set; }

        // Navigation properties
        public Role Role { get; set; }

        // Computed properties
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Id => SupabaseUid.ToString(); // For compatibility with Identity extensions
    }
}
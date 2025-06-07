using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Principal;
using Supabase.Postgrest.Models;

namespace IT_Solution_Platform.Models
{
    /// <summary>
    /// Represents a user entity in the database, mapping to the public.users table.
    /// </summary>

    public class User : BaseModel, IIdentity
    {
        /// <summary>
        /// The unique identifier for the user (auto-incremented).
        /// </summary>
        [Key]
        [Column("user_id")]
        public int user_id { get; set; }

        /// <summary>
        /// The role ID associated with the user (foreign key to roles table).
        /// </summary>
        [Column("role_id")]
        public int role_id { get; set; }

        /// <summary>
        /// The user's email address (unique).
        /// </summary>
        [Required]
        [StringLength(255)]
        [EmailAddress]
        [Column("email")]
        public string email { get; set; }

        /// <summary>
        /// The hashed password (managed by Supabase in this implementation).
        /// </summary>
        [Required]
        [StringLength(255)]
        [Column("password_hash")]
        public string password_hash { get; set; }

        /// <summary>
        /// The user's first name.
        /// </summary>
        [Required]
        [StringLength(100)]
        [Column("first_name")]
        public string first_name { get; set; }

        /// <summary>
        /// The user's last name.
        /// </summary>
        [Required]
        [StringLength(100)]
        [Column("last_name")]
        public string last_name { get; set; }

        /// <summary>
        /// The user's phone number (optional).
        /// </summary>
        [StringLength(20)]
        [Phone]
        [Column("phone_number")]
        public string phone_number { get; set; }

        /// <summary>
        /// The URL or path to the user's profile picture (optional).
        /// </summary>
        [StringLength(255)]
        [Column("profile_picture")]
        public string profile_picture { get; set; }

        /// <summary>
        /// Indicates whether the user account is active.
        /// </summary>
        [Column("is_active")]
        public bool is_active { get; set; } = true;

        /// <summary>
        /// The timestamp of the user's last login.
        /// </summary>
        [Column("last_login")]
        public DateTime? last_login { get; set; }

        /// <summary>
        /// The token used for password reset (optional).
        /// </summary>
        [StringLength(255)]
        [Column("reset_token")]
        public string reset_token { get; set; }

        /// <summary>
        /// The expiration timestamp for the password reset token.
        /// </summary>
        [Column("reset_token_expires")]
        public DateTime? reset_token_expires { get; set; }

        /// <summary>
        /// The timestamp when the user was created.
        /// </summary>
        [Column("created_at")]
        public DateTime created_at { get; set; }

        /// <summary>
        /// The timestamp when the user was last updated.
        /// </summary>
        [Column("updated_at")]
        public DateTime updated_at { get; set; }

        /// <summary>
        /// The Supabase user ID (unique, links to Supabase auth).
        /// </summary>
        [StringLength(255)]
        [Column("supabase_uid")]
        public Guid supabase_uid { get; set; }

        // Navigation properties
        public Role Role { get; set; }

        // Computed properties
        public string FullNameS => $"{first_name} {last_name}".Trim();
        public string Id => supabase_uid.ToString(); // For compatibility with Identity extensions

        // IIdentity Implementation
        public string AuthenticationType => "SupabaseAuth";
        public bool IsAuthenticated => !string.IsNullOrEmpty(Id);
        public String Name => email;

        public String FullName => FullNameS;
    }
}
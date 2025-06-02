using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.WebSockets;
using System.Web;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace IT_Solution_Platform.Models
{
    /// <summary>
    /// View Model for the signup form
    /// </summary>
    public class SignUpViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at most {1} characters long.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at most {1} characters long.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [StringLength(20, ErrorMessage = "The {0} must be at most {1} characters long.")]
        [Phone(ErrorMessage = "Invalid phone number.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

    }

    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Email")]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// View model for the resend verification email form.
    /// </summary>
    public class ResendVerificationViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [StringLength(255)]
        public string Email { get; set; }
    }


    public class VerificationSentModel
    {
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [StringLength(255)]
        public string Email { get; set; }
        public string Message { get; set; }

    }

    public class VerificationFailedViewModel
    {
        public string Message { get; set; }
    }

    public class ForgotPasswordViewModel
    {

        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [StringLength(255)]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }

    /// <summary>
    /// View model for displaying general messages to users
    /// </summary>
    public class MessageViewModel
    {
        public string Message { get; set; }
        public string Title { get; set; }
        public MessageType Type { get; set; } = MessageType.Info;
    }

    /// <summary>
    /// View model for the reset password form
    /// </summary>
    public class ResetPasswordViewModel
    {
        [Required]
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }

        [Required(ErrorMessage = "New Password is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }


    /// <summary>
    /// View model for changing password (authenticated user)
    /// </summary>
    /// <summary>
    /// View model for changing password (authenticated users)
    /// </summary>
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }


    /// <summary>
    ///  PenTesting Request View Model
    /// </summary>
    public class PenTestingRequestViewModel 
    {
        // Hidden field to store THE service ID for penTesting
        // This would typically be fetched on the GET  request and passed to the view
        public int ServiceId { get; set; }

        [Required(ErrorMessage = "Please specify the target system or application ")]
        [Display(Name = "Target System/Application (Url, IP Range, etc.)")]
        public string TargetSystem { get; set; }

        [Required(ErrorMessage = "Please provide a description of the scope.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Scope Description (What is in/out of scope?")]
        public string ScopeDescription { get; set; }

        [Required(ErrorMessage = "Please select the testing objectives.")]
        [Display(Name = "Testing Objectives")]
        public string TestingObjectives { get; set; } // Could be dropdown or checkboxes in the view

        [Display(Name = "Preferred Testing Start Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public System.DateTime? PreferredStartDate { get; set; }

        [Display(Name = "Preferred Testing End Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public System.DateTime? PreferredEndDate { get; set; }

        // User details might be pre-filled if the user is logged in
        [Required(ErrorMessage = "Please provide a primary contact name.")]
        [Display(Name = "Primary Contact Name")]
        public string PrimaryContactName { get; set; }

        [Required(ErrorMessage = "Please provide a primary contact email.")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        [Display(Name = "Primary Contact Email")]
        public string PrimaryContactEmail { get; set; }

        [Phone(ErrorMessage = "Invalid Phone Number")]
        [Display(Name = "Primary Contact Phone (Optional)")]
        public string PrimaryContactPhone { get; set; }

        [Display(Name = "Compliance Requirements (e.g., PCI-DSS, HIPAA)")]
        public string ComplianceRequirements { get; set; } // Could be a dropdown

        [Display(Name = "Supporting Documents (Optional, max 5MB each, .pdf, .doc, .docx, .txt)")]
        // Use HttpPostedFileBase for MVC 5 / .NET Framework
        public IEnumerable<HttpPostedFileBase> SupportingDocuments { get; set; }

        [DataType(DataType.MultilineText)]
        [Display(Name = "Additional Notes (Optional)")]
        public string AdditionalNotes { get; set; }
    }


    [Table("service_requests")]
    public class ServiceRequest : BaseModel
    {
        [PrimaryKey("request_id")] // Assuming auto-incrementing PK
        public int RequestId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("service_id")]
        public int ServiceId { get; set; }

        [Column("package_id")]
        public int? PackageId { get; set; }

        [Column("status_id")]
        public int StatusId { get; set; }

        [Column("request_details")]
        public string RequestDetails { get; set; }

        [Column("priority")]
        public short? Priority { get; set; }

        [Column("requested_date")]
        public DateTime? RequestedDate { get; set; }

        [Column("approved_by")]
        public int? ApprovedBy { get; set; }

        [Column("approved_date")]
        public DateTime? ApprovedDate { get; set; }

        [Column("completion_date")]
        public DateTime? CompletionDate { get; set; }

        [Column("total_amount")]
        public decimal? TotalAmount { get; set; }

        [Column("notes")]
        public string Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("service_request_documents")]
    public class ServiceRequestDocument : BaseModel
    {
        [PrimaryKey("document_id", true)]
        public int DocumentId { get; set; }

        [Column("request_id")]
        [Reference(typeof(ServiceRequest))]
        public int RequestId { get; set; }

        [Column("file_name")]
        public string FileName { get; set; }

        [Column("storage_path")]
        public string StoragePath { get; set; }

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; }
    }


    // ===== ENUMS =====

    /// <summary>
    /// Enum for message types
    /// </summary>
    public enum MessageType
    {
        Info,
        Success,
        Warning,
        Error
    }
    /// <summary>
    /// Role model representing user roles
    /// </summary>
    public class Role
    {
        public int role_id { get; set; }
        public string role_name { get; set; }
        public string description { get; set; }
        public DateTime created_at { get; set; }
    }
}




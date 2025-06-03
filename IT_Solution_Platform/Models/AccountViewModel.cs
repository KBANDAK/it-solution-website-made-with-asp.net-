using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
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


    public class MobileWebAppRequestViewModel
    {
        public int ServiceId { get; set; }

        [Required(ErrorMessage = "Project Name is required.")]
        [Display(Name = "Project Name")]
        [StringLength(100, ErrorMessage = "Project Name cannot exceed 100 characters.")]
        public string ProjectName { get; set; }

        [Required(ErrorMessage = "Project Description is required.")]
        [Display(Name = "Project Description")]
        [StringLength(2000, ErrorMessage = "Project Description cannot exceed 2000 characters.")]
        [DataType(DataType.MultilineText)]
        public string ProjectDescription { get; set; }

        [Required(ErrorMessage = "Platform is required.")]
        [Display(Name = "Platform")]
        public string Platform { get; set; }

        [Required(ErrorMessage = "Development Type is required.")]
        [Display(Name = "Development Type")]
        public string DevelopmentType { get; set; }

        [Display(Name = "Preferred Start Date")]
        [DataType(DataType.Date)]
        public DateTime? PreferredStartDate { get; set; }

        [Display(Name = "Preferred End Date")]
        [DataType(DataType.Date)]
        public DateTime? PreferredEndDate { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [Display(Name = "Full Name")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        public string PrimaryContactName { get; set; }

        [Required(ErrorMessage = "Email Address is required.")]
        [Display(Name = "Email Address")]
        [DataType(DataType.EmailAddress)]
        [StringLength(255, ErrorMessage = "Email Address cannot exceed 255 characters.")]
        [EmailAddress(ErrorMessage = "Invalid Email Address.")]
        public string PrimaryContactEmail { get; set; }

        [Display(Name = "Phone Number")]
        [DataType(DataType.PhoneNumber)]
        [StringLength(20, ErrorMessage = "Phone Number cannot exceed 20 characters.")]
        [Phone(ErrorMessage = "Invalid Phone Number.")]
        public string PrimaryContactPhone { get; set; }

        [Display(Name = "Supporting Documents")]
        public IEnumerable<HttpPostedFileBase> SupportingDocuments { get; set; }

        [Display(Name = "Additional Notes")]
        [StringLength(1000, ErrorMessage = "Additional Notes cannot exceed 1000 characters.")]
        [DataType(DataType.MultilineText)]
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
        [PrimaryKey("document_id")]
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



    public class NetworkServiceModel
    {
        // Service Information
        public int ServiceId { get; set; }

        [Display(Name = "Request Type")]
        [Required(ErrorMessage = "Please select a request type")]
        public NetworkRequestType RequestType { get; set; }

        [Display(Name = "Priority Level")]
        [Required(ErrorMessage = "Please select a priority level")]
        public PriorityLevel Priority { get; set; }

        // Contact Information
        [Display(Name = "Primary Contact Name")]
        [Required(ErrorMessage = "Primary contact name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string PrimaryContactName { get; set; }

        [Display(Name = "Primary Contact Email")]
        [Required(ErrorMessage = "Primary contact email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string PrimaryContactEmail { get; set; }

        [Display(Name = "Primary Contact Phone")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string PrimaryContactPhone { get; set; }

        [Display(Name = "Department")]
        [Required(ErrorMessage = "Department is required")]
        [StringLength(100, ErrorMessage = "Department name cannot exceed 100 characters")]
        public string Department { get; set; }

        // Network Details
        [Display(Name = "Location/Building")]
        [Required(ErrorMessage = "Location is required")]
        [StringLength(200, ErrorMessage = "Location cannot exceed 200 characters")]
        public string Location { get; set; }

        [Display(Name = "Room/Office Number")]
        [StringLength(50, ErrorMessage = "Room number cannot exceed 50 characters")]
        public string RoomNumber { get; set; }

        [Display(Name = "Number of Network Ports Needed")]
        [Range(1, 100, ErrorMessage = "Number of ports must be between 1 and 100")]
        public int? NumberOfPorts { get; set; }

        [Display(Name = "Network Port Type")]
        public NetworkPortType? PortType { get; set; }

        [Display(Name = "Network Speed Required")]
        public NetworkSpeed? NetworkSpeed { get; set; }

        [Display(Name = "VLAN Assignment")]
        [StringLength(100, ErrorMessage = "VLAN assignment cannot exceed 100 characters")]
        public string VlanAssignment { get; set; }

        // Access and Security
        [Display(Name = "Wireless Access Required")]
        public bool WirelessAccessRequired { get; set; }

        [Display(Name = "Network Name (SSID)")]
        [StringLength(100, ErrorMessage = "Network name cannot exceed 100 characters")]
        public string NetworkName { get; set; }

        [Display(Name = "Special Security Requirements")]
        public bool SpecialSecurityRequired { get; set; }

        [Display(Name = "Security Requirements Details")]
        [StringLength(500, ErrorMessage = "Security details cannot exceed 500 characters")]
        public string SecurityRequirementsDetails { get; set; }

        // Equipment and Hardware
        [Display(Name = "Equipment to be Connected")]
        [StringLength(500, ErrorMessage = "Equipment details cannot exceed 500 characters")]
        public string EquipmentDetails { get; set; }

        [Display(Name = "Hardware Installation Required")]
        public bool HardwareInstallationRequired { get; set; }

        [Display(Name = "Hardware Details")]
        [StringLength(500, ErrorMessage = "Hardware details cannot exceed 500 characters")]
        public string HardwareDetails { get; set; }

        // Timing and Scheduling
        [Display(Name = "Requested Completion Date")]
        [DataType(DataType.Date)]
        public DateTime? RequestedCompletionDate { get; set; }

        [Display(Name = "Is this request urgent?")]
        public bool IsUrgent { get; set; }

        [Display(Name = "Urgency Justification")]
        [StringLength(500, ErrorMessage = "Urgency justification cannot exceed 500 characters")]
        public string UrgencyJustification { get; set; }

        [Display(Name = "Preferred Installation Time")]
        public PreferredTime? PreferredInstallationTime { get; set; }

        [Display(Name = "Available Days")]
        public List<DayOfWeek> AvailableDays { get; set; } = new List<DayOfWeek>();

        // Additional Information
        [Display(Name = "Business Justification")]
        [Required(ErrorMessage = "Business justification is required")]
        [StringLength(1000, ErrorMessage = "Business justification cannot exceed 1000 characters")]
        public string BusinessJustification { get; set; }

        [Display(Name = "Additional Notes")]
        [StringLength(1000, ErrorMessage = "Additional notes cannot exceed 1000 characters")]
        public string AdditionalNotes { get; set; }

        [Display(Name = "Budget Code/Account")]
        [StringLength(50, ErrorMessage = "Budget code cannot exceed 50 characters")]
        public string BudgetCode { get; set; }

        // File Attachments
        [Display(Name = "Network Diagram")]
        public HttpPostedFileBase NetworkDiagram { get; set; }

        [Display(Name = "Floor Plan")]
        public HttpPostedFileBase FloorPlan { get; set; }

        [Display(Name = "Additional Documents")]
        public List<HttpPostedFileBase> AdditionalDocuments { get; set; } = new List<HttpPostedFileBase>();

        // Approval and Authorization
        [Display(Name = "Manager/Supervisor Name")]
        [StringLength(100, ErrorMessage = "Manager name cannot exceed 100 characters")]
        public string ManagerName { get; set; }

        [Display(Name = "Manager Email")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string ManagerEmail { get; set; }

        [Display(Name = "I acknowledge that this request may require approval")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must acknowledge the approval requirement")]
        public bool AcknowledgeApproval { get; set; }

        // System Fields (typically hidden)
        public DateTime? SubmittedDate { get; set; }
        public string SubmittedBy { get; set; }
        public int? RequestId { get; set; }
    }

    public class ProfileViewModel
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public string RoleName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Computed properties for the view
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Initials => $"{FirstName?.FirstOrDefault()}{LastName?.FirstOrDefault()}".ToUpper();
        public string DisplayFirstName => string.IsNullOrEmpty(FirstName) ? "Not provided" : FirstName;
        public string DisplayLastName => string.IsNullOrEmpty(LastName) ? "Not provided" : LastName;
        public string DisplayPhoneNumber => string.IsNullOrEmpty(PhoneNumber) ? "Not provided" : PhoneNumber;
        public string DisplayEmail => string.IsNullOrEmpty(Email) ? "Not provided" : Email;
        public string DisplayRoleName => string.IsNullOrEmpty(RoleName) ? "User" : RoleName;
        public string FormattedCreatedDate => CreatedAt?.ToString("MMMM dd, yyyy") ?? "Unknown";
    }


    // Enums for dropdown options
    public enum NetworkRequestType
    {
        [Display(Name = "New Network Connection")]
        NewConnection = 1,

        [Display(Name = "Network Port Addition")]
        PortAddition = 2,

        [Display(Name = "Network Modification")]
        NetworkModification = 3,

        [Display(Name = "Wireless Access Setup")]
        WirelessSetup = 4,

        [Display(Name = "Network Troubleshooting")]
        Troubleshooting = 5,

        [Display(Name = "Network Security Configuration")]
        SecurityConfiguration = 6,

        [Display(Name = "VLAN Configuration")]
        VlanConfiguration = 7,

        [Display(Name = "Network Equipment Installation")]
        EquipmentInstallation = 8,

        [Display(Name = "Other")]
        Other = 9
    }

    public enum PriorityLevel
    {
        [Display(Name = "Low")]
        Low = 1,

        [Display(Name = "Medium")]
        Medium = 2,

        [Display(Name = "High")]
        High = 3,

        [Display(Name = "Critical")]
        Critical = 4
    }

    public enum NetworkPortType
    {
        [Display(Name = "Ethernet (RJ45)")]
        Ethernet = 1,

        [Display(Name = "Fiber Optic")]
        FiberOptic = 2,

        [Display(Name = "Coaxial")]
        Coaxial = 3,

        [Display(Name = "USB")]
        USB = 4,

        [Display(Name = "Other")]
        Other = 5
    }

    public enum NetworkSpeed
    {
        [Display(Name = "10 Mbps")]
        Speed10Mbps = 10,

        [Display(Name = "100 Mbps")]
        Speed100Mbps = 100,

        [Display(Name = "1 Gbps")]
        Speed1Gbps = 1000,

        [Display(Name = "10 Gbps")]
        Speed10Gbps = 10000,

        [Display(Name = "Other")]
        Other = -1
    }

    public enum PreferredTime
    {
        [Display(Name = "Business Hours (8 AM - 5 PM)")]
        BusinessHours = 1,

        [Display(Name = "After Hours (5 PM - 8 AM)")]
        AfterHours = 2,

        [Display(Name = "Weekend")]
        Weekend = 3,

        [Display(Name = "Anytime")]
        Anytime = 4
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




using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace IT_Solution_Platform.Models
{
    /// <summary>
    ///     Service category model 
    /// </summary>
    /// 
    [Supabase.Postgrest.Attributes.Table("service_categories")]
    public class ServiceCategory : BaseModel
    {
        [PrimaryKey("category_id")]
        public int CategoryId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("icon")]
        public string Icon { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }


    /// <summary>
    /// Service Model
    /// </summary>
    /// 
    [Table("services")]
    public class Service : BaseModel
    {
        [PrimaryKey("service_id")]
        public int ServiceId { get; set; }

        [Column("category_id")]
        public int CategoryId { get; set; }

        [Column("name")]
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("price_per_hour")]
        public decimal? PricePerHour { get; set; }

        [Column("estimated_hours")]
        public int? EstimatedHours { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation property (not mapped to database)
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public ServiceCategory Category { get; set; }

        // Computed properties
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal? EstimatedCost => PricePerHour.HasValue && EstimatedHours.HasValue
            ? PricePerHour * EstimatedHours
            : (decimal?)null;


        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string FormattedPricePerHour => PricePerHour?.ToString("C") ?? "N/A";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string FormattedEstimatedCost => EstimatedCost?.ToString("C") ?? "N/A";
    }


    /// <summary>
    /// Request status model
    /// </summary>
    /// 
    [Table("request_statuses")]
    public class RequestStatus : BaseModel
    {
        [PrimaryKey("status_id")]
        public int StatusId { get; set; }

        [Column("status_name")]
        [Required]
        [StringLength(50)]
        public string StatusName { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("color_code")]
        [StringLength(7)]
        public string ColorCode { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }


    /// <summary>
    /// Service Request Model
    /// </summary>
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

        // Navigation properties (not mapped to database)
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public Service Service { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public RequestStatus Status { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public User User { get; set; } // Assuming User is another model representing the user who made the request
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [JsonIgnore]
        public int ApprovedByUser { get; set; } // Assuming User is another model representing the user who approved the request

        // Computed properties for display
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string PriorityText
        {
            get
            {
                if (Priority == 1) return "Low";
                if (Priority == 2) return "Medium";
                if (Priority == 3) return "High";
                if (Priority == 4) return "Critical";
                return "Unknown";
            }
        }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string FormattedRequestedDate => RequestedDate?.ToString("MMM dd, yyyy") ?? "";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string FormattedApprovedDate => ApprovedDate?.ToString("MMM dd, yyyy") ?? "";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string FormattedCompletionDate => CompletionDate?.ToString("MMM dd, yyyy") ?? "";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string FormattedTotalAmount => TotalAmount?.ToString("C") ?? "N/A";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string StatusName => Status?.StatusName ?? "Unknown";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string ServiceName => Service?.Name ?? "Unknown Service";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string CategoryName => Service?.Category?.Name ?? "";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string ApproverFullName => ApprovedByUser != null ? $"" : "";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int ProgressPercentage
        {
            get
            {
                var status = Status?.StatusName?.ToLowerInvariant();
                if (status == "submitted") return 25;
                if (status == "pending") return 50;
                if (status == "inprogress") return 75;
                if (status == "completed") return 100;
                if (status == "rejected") return 0;
                return 0;
            }
        }
    }


    /// <summary>
    /// Enhanced view model for service requesat details
    /// </summary>
    /// 
    public class ServiceRequestDetailViewModel
    {
        public int request_id { get; set; }
        public int? user_id { get; set; }
        public int? service_id { get; set; }
        public int? package_id { get; set; }
        public int? status_id { get; set; }
        public string request_details { get; set; }
        public short? priority { get; set; }
        public DateTime? requested_date { get; set; }
        public int? approved_by { get; set; }
        public DateTime? approved_date { get; set; }
        public DateTime? completion_date { get; set; }
        public decimal? total_amount { get; set; }
        public string notes { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }

        // Service details
        public string service_name { get; set; }
        public string service_description { get; set; }
        public decimal? service_price_per_hour { get; set; }
        public int? service_estimated_hours { get; set; }

        // Category details
        public string category_name { get; set; }
        public string category_description { get; set; }
        public string category_icon { get; set; }

        // Status details
        public string status_name { get; set; }
        public string status_description { get; set; }
        public string status_color_code { get; set; }

        // User details
        public string user_first_name { get; set; }
        public string user_last_name { get; set; }
        public string user_email { get; set; }

        // Approver details
        public string approver_first_name { get; set; }
        public string approver_last_name { get; set; }
        public string approver_email { get; set; }


        // Computed properties for display
        public string PriorityText
        {
            get
            {
                if (priority == 1) return "Low";
                if (priority == 2) return "Medium";
                if (priority == 3) return "High";
                if (priority == 4) return "Critical";
                return "Unknown";
            }
        }

        public string FormattedRequestedDate => requested_date?.ToString("MMM dd, yyyy HH:mm") ?? "";
        public string FormattedApprovedDate => approved_date?.ToString("MMM dd, yyyy HH:mm") ?? "";
        public string FormattedCompletionDate => completion_date?.ToString("MMM dd, yyyy HH:mm") ?? "";
        public string FormattedTotalAmount => total_amount?.ToString("C") ?? "N/A";
        public string FormattedServicePrice => service_price_per_hour?.ToString("C") ?? "N/A";
        public string UserFullName => !string.IsNullOrEmpty(user_first_name) ? $"{user_first_name} {user_last_name}" : "";
        public string ApproverFullName => !string.IsNullOrEmpty(approver_first_name) ? $"{approver_first_name} {approver_last_name}" : "";


        public int ProgressPercentage
        {
            get
            {
                var status = status_name?.ToLowerInvariant();
                if (status == "submitted") return 25;
                if (status == "pending") return 50;
                if (status == "inprogress") return 75;
                if (status == "completed") return 100;
                if (status == "rejected") return 0;
                return 0;
            }
        }


        public decimal? EstimatedServiceCost => service_price_per_hour.HasValue && service_estimated_hours.HasValue
          ? service_price_per_hour * service_estimated_hours
          : null;

        public string FormattedEstimatedCost => EstimatedServiceCost?.ToString("C") ?? "N/A";
    }

    /// <summary>
    /// Filter parameters for service requests
    /// </summary>
    public class ServiceRequestFilterParams
    {
        public int? UserId { get; set; }
        public int? ServiceId { get; set; }
        public int? CategoryId { get; set; }
        public int? StatusId { get; set; }
        public string StatusName { get; set; }
        public short? Priority { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortBy { get; set; } = "requested_date";
        public bool SortDescending { get; set; } = true;
    }
}
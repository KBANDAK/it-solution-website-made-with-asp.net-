using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using IT_Solution_Platform.Models;

namespace IT_Solution_Platform.Handlers
{
    public class NetworkServiceRequestHandler : IServiceRequestHandler
    {
        public string ServiceType => "NetworkService";

        public Dictionary<string, object> ExtractRequestDetails(object model)
        {
            if (!(model is NetworkServiceModel networkModel))
                throw new ArgumentException("Invalid model type");
            return new Dictionary<string, object>
            {
                { "ServiceId", networkModel.ServiceId },
                { "RequestType", networkModel.RequestType.ToString() },
                { "Priority", networkModel.Priority.ToString() },
                { "PrimaryContactName", networkModel.PrimaryContactName },
                { "PrimaryContactEmail", networkModel.PrimaryContactEmail },
                { "PrimaryContactPhone", networkModel.PrimaryContactPhone },
                { "Department", networkModel.Department },
                { "Location", networkModel.Location },
                { "RoomNumber", networkModel.RoomNumber },
                { "NumberOfPorts", networkModel.NumberOfPorts },
                { "PortType", networkModel.PortType?.ToString() },
                { "NetworkSpeed", networkModel.NetworkSpeed?.ToString() },
                { "VlanAssignment", networkModel.VlanAssignment },
                { "WirelessAccessRequired", networkModel.WirelessAccessRequired },
                { "NetworkName", networkModel.NetworkName },
                { "SpecialSecurityRequired", networkModel.SpecialSecurityRequired },
                { "SecurityRequirementsDetails", networkModel.SecurityRequirementsDetails },
                { "EquipmentDetails", networkModel.EquipmentDetails },
                { "HardwareInstallationRequired", networkModel.HardwareInstallationRequired },
                { "HardwareDetails", networkModel.HardwareDetails },
                { "RequestedCompletionDate", networkModel.RequestedCompletionDate?.ToString("yyyy-MM-dd") },
                { "IsUrgent", networkModel.IsUrgent },
                { "UrgencyJustification", networkModel.UrgencyJustification },
                { "PreferredInstallationTime", networkModel.PreferredInstallationTime?.ToString() },
                { "AvailableDays", networkModel.AvailableDays?.Select(d => d.ToString()).ToList() },
                { "BusinessJustification", networkModel.BusinessJustification },
                { "AdditionalNotes", networkModel.AdditionalNotes },
                { "BudgetCode", networkModel.BudgetCode },
                { "ManagerName", networkModel.ManagerName },
                { "ManagerEmail", networkModel.ManagerEmail },
                { "AcknowledgeApproval", networkModel.AcknowledgeApproval },
                { "SubmittedDate", networkModel.SubmittedDate?.ToString("yyyy-MM-dd") },
                { "SubmittedBy", networkModel.SubmittedBy },
                { "RequestId", networkModel.RequestId }
            };
        }

        public HttpPostedFileBase[] GetSupportingDocuments(object model)
        {
            var networkModel = model as NetworkServiceModel;
            return networkModel?.AdditionalDocuments?.ToArray() ?? Array.Empty<HttpPostedFileBase>();
        }

        public int GetServiceId(object model)
        {
            // Fixed: Use NetworkServiceModel instead of MobileWebAppRequestViewModel
            return (model as NetworkServiceModel)?.ServiceId ?? 0;
        }

        public bool ValidateModel(object model, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Fixed: Validate NetworkServiceModel instead of MobileWebAppRequestViewModel
            if (!(model is NetworkServiceModel networkModel))
            {
                errorMessage = "Invalid model type";
                return false;
            }

            // Add your NetworkServiceModel-specific validation here
            if (string.IsNullOrEmpty(networkModel.PrimaryContactName))
            {
                errorMessage = "Primary Contact Name is required";
                return false;
            }

            if (string.IsNullOrEmpty(networkModel.PrimaryContactEmail))
            {
                errorMessage = "Primary Contact Email is required";
                return false;
            }

            if (string.IsNullOrEmpty(networkModel.Department))
            {
                errorMessage = "Department is required";
                return false;
            }

            if (string.IsNullOrEmpty(networkModel.Location))
            {
                errorMessage = "Location is required";
                return false;
            }

            // Add more validation rules as needed for NetworkServiceModel
            return true;
        }
    }
}
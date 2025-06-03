using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using IT_Solution_Platform.Models;

namespace IT_Solution_Platform.Handlers
{
    public class MobileWebAppRequestHandler : IServiceRequestHandler
    {
        public string ServiceType => "MobileWebApp";

        public Dictionary<string, object> ExtractRequestDetails(object model)
        {
            if (!(model is MobileWebAppRequestViewModel mobileWebAppModel))
                throw new ArgumentException("Invalid model type");

            return new Dictionary<string, object>
            {
                { "ProjectName", mobileWebAppModel.ProjectName },
                { "ProjectDescription", mobileWebAppModel.ProjectDescription },
                { "Platform", mobileWebAppModel.Platform },
                { "DevelopmentType", mobileWebAppModel.DevelopmentType },
                { "PreferredStartDate", mobileWebAppModel.PreferredStartDate?.ToString("yyyy-MM-dd") },
                { "PreferredEndDate", mobileWebAppModel.PreferredEndDate?.ToString("yyyy-MM-dd") },
                { "PrimaryContactName", mobileWebAppModel.PrimaryContactName },
                { "PrimaryContactEmail", mobileWebAppModel.PrimaryContactEmail },
                { "PrimaryContactPhone", mobileWebAppModel.PrimaryContactPhone },
                { "AdditionalNotes", mobileWebAppModel.AdditionalNotes }
            };
        }

        public HttpPostedFileBase[] GetSupportingDocuments(object model)
        {
            return (HttpPostedFileBase[])((model as MobileWebAppRequestViewModel)?.SupportingDocuments);
        }

        public int GetServiceId(object model)
        {
            return (model as MobileWebAppRequestViewModel)?.ServiceId ?? 0;
        }

        public bool ValidateModel(object model, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!(model is MobileWebAppRequestViewModel mobileWebAppModel))
            {
                errorMessage = "Invalid model type";
                return false;
            }

            if (string.IsNullOrEmpty(mobileWebAppModel.ProjectName))
            {
                errorMessage = "Project Name is required";
                return false;
            }

            // Add more validation as needed
            return true;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IT_Solution_Platform.Handlers
{
    public interface IServiceRequestHandler
    {
        string ServiceType { get; }
        Dictionary<string, object> ExtractRequestDetails(object model);
        HttpPostedFileBase[] GetSupportingDocuments(object model);
        int GetServiceId(object model);
        bool ValidateModel(object model, out string errorMessage);
    }
}
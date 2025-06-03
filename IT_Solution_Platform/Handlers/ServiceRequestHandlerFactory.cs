using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using IT_Solution_Platform.Models;

namespace IT_Solution_Platform.Handlers
{
    public class ServiceRequestHandlerFactory
    {
        private readonly Dictionary<Type, IServiceRequestHandler> _handlers;

        public ServiceRequestHandlerFactory()
        {
            _handlers = new Dictionary<Type, IServiceRequestHandler>
        {
            { typeof(PenTestingRequestViewModel), new PenTestingRequestHandler() },
            { typeof(MobileWebAppRequestViewModel), new MobileWebAppRequestHandler() },
            { typeof(NetworkServiceModel), new NetworkServiceRequestHandler() }
            // Add new handlers here as you add more service types
        };
        }

        public IServiceRequestHandler GetHandler(Type modelType)
        {
            return _handlers.TryGetValue(modelType, out var handler) ? handler : null;
        }
    }
}
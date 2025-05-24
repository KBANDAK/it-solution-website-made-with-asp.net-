using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using IT_Solution_Platform.App_Start;
using IT_Solution_Platform.Services;
using Unity;
using Unity.AspNet.Mvc;

namespace IT_Solution_Platform
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Load environment variables from .env file
            EnvLoader.LoadEnv();
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Configure anti-forgery to use 'sub' claim (optional)
            AntiForgeryConfig.UniqueClaimTypeIdentifier = "sub";
        }
    }
}

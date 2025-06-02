using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using IT_Solution_Platform.App_Start;
using IT_Solution_Platform.Services;
using Supabase;
using Unity;
using Unity.AspNet.Mvc;
using Unity.Lifetime;

namespace IT_Solution_Platform
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected async void Application_Start()
        {
            // Load environment variables from .env file
            EnvLoader.LoadEnv();
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            // Configure anti-forgery to use NameIdentifier claim
            AntiForgeryConfig.UniqueClaimTypeIdentifier = ClaimTypes.NameIdentifier;
            // Initialize Supabase
            try
            {
                await SupabaseConfig.Initialize();
                System.Diagnostics.Debug.WriteLine("Supabase initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Supabase initialization failed: {ex.Message}");
                // Log error or handle as appropriate
            }
        }
    }
}

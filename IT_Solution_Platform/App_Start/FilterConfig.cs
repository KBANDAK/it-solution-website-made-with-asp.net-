using System.Web;
using System.Web.Mvc;
using IT_Solution_Platform.Filters;
using IT_Solution_Platform.Services;

namespace IT_Solution_Platform
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new JwtAuthenticationFilter(
                DependencyResolver.Current.GetService<SupabaseAuthService>(),
                DependencyResolver.Current.GetService<SupabaseDatabase>()) // Added to fetch user details
            );
        }
    }
}

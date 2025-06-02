using System.Web;
using System.Web.Mvc;
using IT_Solution_Platform.Services;
using Supabase;

namespace IT_Solution_Platform
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}

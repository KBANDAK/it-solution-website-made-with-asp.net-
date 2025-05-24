using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using IT_Solution_Platform.Services;

namespace IT_Solution_Platform.Filters
{
    public class JwtAuthenticationFilter : ActionFilterAttribute
    {
        private readonly SupabaseAuthService _authService;
        private readonly SupabaseDatabase _databaseService; // Added to fetch user details

        public JwtAuthenticationFilter(SupabaseAuthService authService, SupabaseDatabase databaseService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            var accessToken = request.Cookies["access_token"]?.Value;

            if (!string.IsNullOrEmpty(accessToken))
            {
                try
                {
                    // Validate JWT with Supabase (synchronous call via Task.Run)
                    var supabaseUser = Task.Run(() => _authService.GetUserByTokenAsync(accessToken)).Result;

                    if (supabaseUser != null && !string.IsNullOrEmpty(supabaseUser.Id))
                    {
                        // Fetch user details from database to get Fullname
                        var dbUser = _databaseService.ExecuteQuery<Models.User>(
                            "SELECT * FROM users WHERE supabase_uid = CAST(@SupabaseUid AS uuid)",
                            new { SupabaseUid = supabaseUser.Id }).FirstOrDefault();

                        // Create ClaimsIdentity with required claims
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.Name, supabaseUser.Email ?? ""),
                            new Claim(ClaimTypes.NameIdentifier, supabaseUser.Id), // Ensure NameIdentifier is set
                            new Claim("sub", supabaseUser.Id),
                            new Claim("email", supabaseUser.Email ?? "")
                        };
                        var identity = new ClaimsIdentity(claims, "Jwt");
                        var principal = new ClaimsPrincipal(identity);

                        // Set the user in HttpContext
                        filterContext.HttpContext.User = principal;
                        System.Threading.Thread.CurrentPrincipal = principal;

                        // Store Fullname in Session for headerPartial
                        filterContext.HttpContext.Session["Fullname"] = dbUser != null && !string.IsNullOrEmpty(dbUser.FullName)
                            ? dbUser.FullName
                            : supabaseUser.Email ?? "User";
                    }
                    else
                    {
                        ClearInvalidTokenCookie(filterContext);
                    }
                }
                catch (Exception)
                {
                    // Invalid token; clear the cookie
                    ClearInvalidTokenCookie(filterContext);
                }
            }

            base.OnActionExecuting(filterContext);
        }

        private void ClearInvalidTokenCookie(ActionExecutingContext filterContext)
        {
            var cookie = new HttpCookie("access_token")
            {
                Expires = DateTime.UtcNow.AddDays(-1),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            };
            filterContext.HttpContext.Response.Cookies.Add(cookie);
        }
    }
}
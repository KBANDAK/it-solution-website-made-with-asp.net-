using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;

namespace IT_Solution_Platform
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Configure cookie authentication
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = "ApplicationCookie",
                LoginPath = new PathString("/Account/Login"),
                ExpireTimeSpan = TimeSpan.FromHours(1),
                SlidingExpiration = true,
                CookieName = "SupabaseAuth",
                CookieSecure = CookieSecureOption.SameAsRequest
            });
        }
    }
}
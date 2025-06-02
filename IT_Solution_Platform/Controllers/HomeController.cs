using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using IT_Solution_Platform.Services;

namespace IT_Solution_Platform.Controllers
{
    public class HomeController : Controller
    {
        public HomeController(SupabaseAuthService authService, SupabaseDatabase databaseService) 
        {
        }

        // This home page
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [Authorize]
        public ActionResult Services()
        {
            ViewBag.Message = "Your services page.";

            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult PrivacyPolicy() 
        {
            return View();
        }
    }
}
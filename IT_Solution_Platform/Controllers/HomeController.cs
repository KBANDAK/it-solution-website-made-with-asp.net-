using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using IT_Solution_Platform.Models;
using IT_Solution_Platform.Services;

namespace IT_Solution_Platform.Controllers
{
    public class HomeController : Controller
    {
        private readonly ContactInquiryService _contactService;
        private readonly AuditLogService _auditLog;
        public HomeController() 
        {
            _contactService = new ContactInquiryService();
            _auditLog = new AuditLogService(_contactService._databaseService);
        }

        // This home page
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Services()
        {
            // Check if user is authenticated
            if (!User.Identity.IsAuthenticated)
            {
                ViewBag.ErrorMessage = "You must be logged in to access the services page.";
                return View("Error");
            }

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
            var model = new ContactViewModle();

            // check if user is authenticated
            if (User.Identity.IsAuthenticated)
            {
                model.IsAuthenticated = true;

                // Get user information from claims
                var claimsPrincipal = User as ClaimsPrincipal;
                if (claimsPrincipal != null)
                {
                    // Get user details from claims
                    var firstName = claimsPrincipal.FindFirst("FirstName")?.Value ?? "";
                    var lastName = claimsPrincipal.FindFirst("LastName")?.Value ?? "";
                    var email = claimsPrincipal.FindFirst(ClaimTypes.Email)?.Value ?? "";

                    // Populate the model with user information
                    model.Name = $"{firstName} {lastName}".Trim();
                    model.Email = email;
                    model.UserDisplayName = model.Name;

                    // If name is empty, try to use email username
                    if (string.IsNullOrWhiteSpace(model.Name) && !string.IsNullOrWhiteSpace(email))
                    {
                        model.Name = email.Split('@')[0];
                        model.UserDisplayName = model.Name;
                    }
                }
                else 
                { 
                    model.IsAuthenticated = false;
                }
            }

            ViewBag.Message = "Your contact page. ";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public ActionResult Contact(ContactViewModle model)
        {
            try 
            {
                if (!ModelState.IsValid)
                {
                    // If user is authenticated, repopulate user info
                    if (User.Identity.IsAuthenticated)
                    {
                        model.IsAuthenticated = true;
                        var claimsPrincipal = User as ClaimsPrincipal;
                        if (claimsPrincipal != null)
                        { 
                            var firstName = claimsPrincipal.FindFirst("FirstName")?.Value ?? "";
                            var lastName = claimsPrincipal.FindFirst("LastName")?.Value ?? "";
                            model.UserDisplayName = $"{firstName} {lastName}".Trim();
                            model.Email = claimsPrincipal.FindFirst(ClaimTypes.Email)?.Value ?? "";
                        }
                    }
                    ViewBag.ErrorMessage = "Please correct the errors below.";
                    return View(model);
                }

                // Create the contact inquiry
                var inquiry = new ContactInquiryModel
                {
                    name = model.Name,
                    email = model.Email,
                    phone = model.Phone,
                    subject = model.Subject,
                    message = model.Message,
                };

                // Get user ID if authenticated
                // Get user ID if authenticated
                int? userId = null;
                if (User.Identity.IsAuthenticated)
                {
                    var claimsPrincipal = User as ClaimsPrincipal;
                    var userIdClaim = claimsPrincipal?.FindFirst("UserId")?.Value;
                    if (int.TryParse(userIdClaim, out int parsedUserId))
                    {
                        userId = parsedUserId;
                    }
                }

                // Save the inquiry
                var success = _contactService.CreateInquiry(
                    inquiry,
                    userId,
                    Request.UserHostAddress,
                    Request.UserAgent
                );

                if (success)
                {
                    ViewBag.SuccessMessage = "Thank you for your message! We'll get back to you soon.";

                    // Clear the form by returning a new model with user info if authenticated
                    var newModel = new ContactViewModle();
                    if (User.Identity.IsAuthenticated)
                    {
                        newModel.IsAuthenticated = true;
                        var claimsPrincipal = User as ClaimsPrincipal;
                        if (claimsPrincipal != null)
                        {
                            var firstName = claimsPrincipal.FindFirst("FirstName")?.Value ?? "";
                            var lastName = claimsPrincipal.FindFirst("LastName")?.Value ?? "";
                            var email = claimsPrincipal.FindFirst(ClaimTypes.Email)?.Value ?? "";

                            newModel.Name = $"{firstName} {lastName}".Trim();
                            newModel.Email = email;
                            newModel.UserDisplayName = newModel.Name;
                        }
                    }

                    return View(newModel);
                }
                else
                {
                    ViewBag.ErrorMessage = "Sorry, there was an error sending your message. Please try again.";
                    return View(model);
                }

            }
            catch (Exception ex)
            {
                // Log the error
                _auditLog.LogAudit(
                    0,
                    "Contact Form Submission Error",
                    "Contact",
                    null,
                    new { Error = ex.Message, model.Name, model.Email },
                    Request.UserHostAddress,
                    Request.UserAgent
                );

                ViewBag.ErrorMessage = "Sorry, there was an unexpected error. Please try again later.";
                return View(model);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult PrivacyPolicy() 
        {
            return View();
        }
    }
}
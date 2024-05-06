using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace u19239752_HW01.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Ride()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Manage()
        {
            ViewBag.Message = "Your contact page.";

            return View("Manage");
        }

        public ActionResult Booking()
        {
            return View("Booking");
        }

        public ActionResult BookPage(string headerText)
        {
            ViewBag.HeaderText = headerText;

            return View();
        }

       // [HttpPost]
        //* public ActionResult YourAction()
        // {
        // string dateTimeValue = Request.Form["datetimepicker"];  // Accessing the value using Request.Form

        // OR

        // Binding the value to a model property
        //  YourModel model = new YourModel();
        // model.DateTimeValue = Request.Form["datetimepicker"];

        // Rest of the action logic
        // }
    }
}
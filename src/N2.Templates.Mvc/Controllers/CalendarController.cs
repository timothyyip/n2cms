using System;
using System.Linq;
using System.Web.Mvc;
using N2.Templates.Mvc.Items.Pages;
using N2.Templates.Mvc.Models;
using N2.Web;
using N2.Web.Mvc;

namespace N2.Templates.Mvc.Controllers
{
	[Controls(typeof(Calendar))]
	public class CalendarController : ContentController<Calendar>
	{
		[NonAction]
		public override ActionResult Index()
		{
			return Index(null);
		}

		public ActionResult Index(DateTime? date)
		{
			var hits = CurrentItem.GetEvents();
			
			if (date != null)
				hits = CurrentItem.GetEvents(date.Value);

			return View(new CalendarModel(CurrentItem, hits.ToList()));
		}
	}
}
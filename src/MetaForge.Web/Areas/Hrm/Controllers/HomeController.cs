using Microsoft.AspNetCore.Mvc;

namespace MetaForge.Web.Areas.Hrm.Controllers;

[Area("Hrm")]
[Route("[area]/[controller]")]
public class HomeController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}

using Microsoft.AspNetCore.Mvc;

namespace MetaForge.Web.Areas.Production.Controllers;

[Area("Production")]
[Route("[area]/[controller]")]
public class HomeController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}

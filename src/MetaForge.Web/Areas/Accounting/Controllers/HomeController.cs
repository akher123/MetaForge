using Microsoft.AspNetCore.Mvc;

namespace MetaForge.Web.Areas.Accounting.Controllers;

[Area("Accounting")]
[Route("[area]/[controller]")]
public class HomeController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}

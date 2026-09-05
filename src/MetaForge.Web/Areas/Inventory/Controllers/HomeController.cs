using Microsoft.AspNetCore.Mvc;

namespace MetaForge.Web.Areas.Inventory.Controllers;

[Area("Inventory")]
[Route("[area]/[controller]")]
public class HomeController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}

using MetaForge.Application.Interfaces;
using MetaForge.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace MetaForge.Web.ViewComponents;

public class BreadcrumbViewComponent : ViewComponent
{
    private readonly INavigationService _navigationService;

    public BreadcrumbViewComponent(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string? currentPage = null, string? requestPath = null)
    {
        var path = requestPath ?? HttpContext.Request.Path.Value ?? "/";
        var trail = await _navigationService.GetBreadcrumbsAsync(path, currentPage, HttpContext.RequestAborted);
        var model = trail.Select(BreadcrumbItem.FromDto).ToList();
        return View(model);
    }
}

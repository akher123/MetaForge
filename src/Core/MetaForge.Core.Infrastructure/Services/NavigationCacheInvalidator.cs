using MetaForge.Application.Interfaces;
using MetaForge.Shared.Constants;
using Microsoft.Extensions.Caching.Memory;

namespace MetaForge.Infrastructure.Services;

public sealed class NavigationCacheInvalidator : INavigationCacheInvalidator
{
    private readonly IMemoryCache _cache;

    public NavigationCacheInvalidator(IMemoryCache cache) => _cache = cache;

    public void InvalidateSidebarMenus() =>
        _cache.Set(AppConstants.SidebarMenuVersionKey, DateTime.UtcNow.Ticks);
}

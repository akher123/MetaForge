using MetaForge.Application.Configuration;
using MetaForge.Shared.Constants;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Shared in-memory cache for admin form metadata used by forms, grids, and validation.
/// </summary>
public class FormMetadataCache : IFormMetadataCache
{
    private static readonly object NotFoundSentinel = new();

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly MetadataCacheOptions _options;

    public FormMetadataCache(
        IUnitOfWork unitOfWork,
        IMemoryCache cache,
        IOptions<MetadataCacheOptions> options)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _options = options.Value;
    }

    public Task<ForgeForm?> GetByCodeAsync(string formCode, CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(CodeKey(formCode), () => _unitOfWork.Forms.GetByCodeAsync(formCode, cancellationToken));

    public Task<ForgeForm?> GetByEntityNameAsync(string entityName, CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(EntityKey(entityName), () => _unitOfWork.Forms.GetByEntityNameAsync(entityName, cancellationToken));

    public void Invalidate(string formCode, string? entityName = null)
    {
        _cache.Remove(CodeKey(formCode));

        if (!string.IsNullOrWhiteSpace(entityName))
            _cache.Remove(EntityKey(entityName));
    }

    private async Task<ForgeForm?> GetOrLoadAsync(string key, Func<Task<ForgeForm?>> loader)
    {
        if (_cache.TryGetValue(key, out object? cached))
        {
            if (cached is ForgeForm cachedForm)
                return cachedForm;

            return null;
        }

        var loaded = await loader();

        if (loaded != null)
        {
            StoreForm(loaded);
            return loaded;
        }

        _cache.Set(key, NotFoundSentinel, NotFoundEntryOptions());
        return null;
    }

    private void StoreForm(ForgeForm form)
    {
        var options = FormEntryOptions();

        _cache.Set(CodeKey(form.Code), form, options);
        _cache.Set(EntityKey(form.EntityName), form, options);
    }

    private MemoryCacheEntryOptions FormEntryOptions()
    {
        var entry = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.AbsoluteExpirationMinutes)
        };

        if (_options.SlidingExpirationMinutes > 0)
            entry.SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes);

        return entry;
    }

    private MemoryCacheEntryOptions NotFoundEntryOptions() =>
        new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.NotFoundExpirationMinutes)
        };

    private static string CodeKey(string formCode) =>
        $"{AppConstants.MetadataCacheKeyPrefix}form:code:{formCode}";

    private static string EntityKey(string entityName) =>
        $"{AppConstants.MetadataCacheKeyPrefix}form:entity:{entityName}";
}

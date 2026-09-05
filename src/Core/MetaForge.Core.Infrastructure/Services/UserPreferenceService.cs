using System.Globalization;
using MetaForge.Domain.Security;
using MetaForge.Shared.Constants;
using MetaForge.Shared.Culture;
using MetaForge.Shared.Exceptions;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Loads and persists per-user UI preferences.
/// </summary>
public class UserPreferenceService : IUserPreferenceService
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly IPreferenceResolver _preferenceResolver;

    public UserPreferenceService(MetaForgeDbContext dbContext, IPreferenceResolver preferenceResolver)
    {
        _dbContext = dbContext;
        _preferenceResolver = preferenceResolver;
    }

    public async Task<string> GetThemeAsync(int userId, CancellationToken cancellationToken = default)
    {
        var effective = await _preferenceResolver.ResolveAsync(userId, cancellationToken);
        return effective.ThemeKey;
    }

    public async Task SetThemeAsync(int userId, string? themeKey, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} was not found.");

        if (string.IsNullOrWhiteSpace(themeKey))
        {
            user.ThemeKey = null;
        }
        else
        {
            if (!AppThemes.IsValid(themeKey))
                throw new BusinessException($"Unknown theme '{themeKey}'.");

            user.ThemeKey = AppThemes.Normalize(themeKey);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetCultureOverrideAsync(int userId, CancellationToken cancellationToken = default)
    {
        var culture = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.CultureOverride)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(culture) ? null : culture;
    }

    public async Task SetCultureAsync(int userId, string? culture, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} was not found.");

        if (string.IsNullOrWhiteSpace(culture))
        {
            user.CultureOverride = null;
        }
        else
        {
            string normalized;
            try
            {
                normalized = CultureCatalog.NormalizeOrThrow(culture);
            }
            catch (CultureNotFoundException)
            {
                throw new BusinessException($"Culture '{culture}' is not supported by the .NET runtime.");
            }

            user.CultureOverride = normalized;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDateFormatsAsync(
        int userId,
        string? dateFormat,
        string? dateTimeFormat,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} was not found.");

        var effective = await _preferenceResolver.ResolveAsync(userId, cancellationToken);
        var culture = effective.Culture;

        if (string.IsNullOrWhiteSpace(dateFormat))
        {
            user.DateFormatOverride = null;
        }
        else if (!DateFormatCatalog.IsValidDateFormat(dateFormat, culture))
        {
            throw new BusinessException($"Date format '{dateFormat}' is not valid for culture '{culture}'.");
        }
        else
        {
            user.DateFormatOverride = dateFormat.Trim();
        }

        if (string.IsNullOrWhiteSpace(dateTimeFormat))
        {
            user.DateTimeFormatOverride = null;
        }
        else if (!DateFormatCatalog.IsValidDateTimeFormat(dateTimeFormat, culture))
        {
            throw new BusinessException($"Date-time format '{dateTimeFormat}' is not valid for culture '{culture}'.");
        }
        else
        {
            user.DateTimeFormatOverride = dateTimeFormat.Trim();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetToSystemDefaultsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} was not found.");

        user.ThemeKey = null;
        user.CultureOverride = null;
        user.DateFormatOverride = null;
        user.DateTimeFormatOverride = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public IReadOnlyList<ThemeOptionDto> GetAvailableThemes() =>
        AppThemes.Catalog.Select(t => new ThemeOptionDto
        {
            Key = t.Key,
            DisplayName = t.DisplayName,
            IsDark = t.IsDark,
            PrimarySwatch = t.PrimarySwatch,
            AccentSwatch = t.AccentSwatch,
            BackgroundSwatch = t.BackgroundSwatch,
            SurfaceSwatch = t.SurfaceSwatch,
            Description = t.Description
        }).ToList();
}

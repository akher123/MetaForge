using MetaForge.Domain.Security;
using MetaForge.Shared.Constants;
using MetaForge.Shared.Exceptions;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Loads and persists per-user UI preferences.
/// </summary>
public class UserPreferenceService : IUserPreferenceService
{
    private readonly MetaForgeDbContext _dbContext;

    public UserPreferenceService(MetaForgeDbContext dbContext) => _dbContext = dbContext;

    public async Task<string> GetThemeAsync(int userId, CancellationToken cancellationToken = default)
    {
        var themeKey = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.ThemeKey)
            .FirstOrDefaultAsync(cancellationToken);

        return AppThemes.Normalize(themeKey);
    }

    public async Task SetThemeAsync(int userId, string themeKey, CancellationToken cancellationToken = default)
    {
        if (!AppThemes.IsValid(themeKey))
            throw new BusinessException($"Unknown theme '{themeKey}'.");

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} was not found.");

        user.ThemeKey = AppThemes.Normalize(themeKey);
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

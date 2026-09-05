using System.Security.Cryptography;
using System.Text;
using MetaForge.Application.Configuration;
using MetaForge.Application.Interfaces;
using MetaForge.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Secure password reset token lifecycle: issue, validate, consume, and notify by email.
/// </summary>
public sealed class PasswordResetService : IPasswordResetService
{
    private readonly MetaForgeDbContext _db;
    private readonly IEmailDispatchService _emailDispatch;
    private readonly ISecurityStampService _securityStampService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SecurityOptions _options;

    public PasswordResetService(
        MetaForgeDbContext db,
        IEmailDispatchService emailDispatch,
        ISecurityStampService securityStampService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<SecurityOptions> options)
    {
        _db = db;
        _emailDispatch = emailDispatch;
        _securityStampService = securityStampService;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public Task SendNewUserInviteAsync(int userId, CancellationToken cancellationToken = default) =>
        SendResetEmailAsync(userId, PasswordResetPurpose.NewUserInvite, cancellationToken);

    public async Task SendForgotPasswordAsync(string emailOrUserName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(emailOrUserName))
            return;

        var normalized = emailOrUserName.Trim();
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.IsActive
                && (u.UserName == normalized || u.Email == normalized),
                cancellationToken);

        if (user == null || string.IsNullOrWhiteSpace(user.Email))
            return;

        await SendResetEmailAsync(user.Id, PasswordResetPurpose.ForgotPassword, cancellationToken);
    }

    public async Task<PasswordResetTokenInfo?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var entity = await FindValidTokenEntityAsync(token, cancellationToken);
        if (entity == null)
            return null;

        return new PasswordResetTokenInfo
        {
            UserId = entity.UserId,
            UserName = entity.User.UserName,
            Purpose = entity.Purpose
        };
    }

    public async Task ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        ValidatePasswordStrength(newPassword);

        var entity = await FindValidTokenEntityAsync(token, cancellationToken)
            ?? throw new BusinessException("This reset link is invalid or has expired.");

        var user = entity.User;
        user.PasswordHash = PasswordHasher.Hash(newPassword);
        entity.UsedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _securityStampService.BumpUserStampAsync(user.Id, cancellationToken);

        await InvalidateUnusedTokensAsync(user.Id, entity.Purpose, entity.Id, cancellationToken);
    }

    private async Task SendResetEmailAsync(int userId, string purpose, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            ?? throw new NotFoundException($"User {userId} not found.");

        if (string.IsNullOrWhiteSpace(user.Email))
            throw new BusinessException("User does not have an email address.");

        var rawToken = await IssueTokenAsync(userId, purpose, cancellationToken);
        var resetLink = BuildResetLink(rawToken);
        var expiresHours = Math.Max(1, _options.PasswordResetTokenLifetimeHours);

        await _emailDispatch.EnqueueFromTemplateAsync(new EmailSendRequest
        {
            TemplateCode = _options.PasswordResetTemplateCode,
            EntityName = nameof(User),
            RecordId = userId,
            ToAddress = user.Email,
            AdditionalTokens = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ResetLink"] = resetLink,
                ["ResetPurpose"] = purpose,
                ["ExpiresHours"] = expiresHours,
                ["UserName"] = user.UserName
            }
        }, cancellationToken);
    }

    private async Task<string> IssueTokenAsync(int userId, string purpose, CancellationToken cancellationToken)
    {
        await InvalidateUnusedTokensAsync(userId, purpose, excludedTokenId: null, cancellationToken);

        var rawToken = GenerateRawToken();
        var entity = new PasswordResetToken
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            Purpose = purpose,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddHours(Math.Max(1, _options.PasswordResetTokenLifetimeHours))
        };

        _db.PasswordResetTokens.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    private async Task<PasswordResetToken?> FindValidTokenEntityAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = HashToken(token.Trim());
        var now = DateTime.UtcNow;

        return await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == hash
                && t.UsedUtc == null
                && t.ExpiresUtc > now
                && t.User.IsActive,
                cancellationToken);
    }

    private async Task InvalidateUnusedTokensAsync(
        int userId,
        string purpose,
        int? excludedTokenId,
        CancellationToken cancellationToken)
    {
        var tokens = await _db.PasswordResetTokens
            .Where(t => t.UserId == userId
                && t.Purpose == purpose
                && t.UsedUtc == null
                && (excludedTokenId == null || t.Id != excludedTokenId))
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var token in tokens)
            token.UsedUtc = now;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private string BuildResetLink(string rawToken)
    {
        var baseUrl = ResolvePublicBaseUrl();
        return $"{baseUrl}/Account/ResetPassword?token={Uri.EscapeDataString(rawToken)}";
    }

    private string ResolvePublicBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            return _options.PublicBaseUrl.TrimEnd('/');

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
            return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

        return "https://localhost";
    }

    private void ValidatePasswordStrength(string password)
    {
        var minLength = Math.Max(6, _options.MinimumPasswordLength);
        if (string.IsNullOrWhiteSpace(password) || password.Length < minLength)
            throw new BusinessException($"Password must be at least {minLength} characters.");
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private string HashToken(string rawToken)
    {
        var input = string.IsNullOrEmpty(_options.TokenPepper)
            ? rawToken
            : $"{rawToken}:{_options.TokenPepper}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }
}

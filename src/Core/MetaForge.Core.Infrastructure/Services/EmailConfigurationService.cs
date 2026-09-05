namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Admin CRUD for email channels, retry policies, and templates.
/// </summary>
public class EmailConfigurationService : IEmailConfigurationService
{
    private readonly MetaForgeDbContext _db;
    private readonly IEntityMetadataDiscoveryService _discoveryService;

    public EmailConfigurationService(
        MetaForgeDbContext db,
        IEntityMetadataDiscoveryService discoveryService)
    {
        _db = db;
        _discoveryService = discoveryService;
    }

    public async Task<IReadOnlyList<EmailChannelListItemDto>> GetChannelsAsync(CancellationToken cancellationToken = default) =>
        await _db.EmailChannels
            .OrderBy(c => c.Name)
            .Select(c => new EmailChannelListItemDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Provider = c.Provider,
                FromAddress = c.FromAddress,
                IsActive = c.IsActive,
                IsDefault = c.IsDefault
            })
            .ToListAsync(cancellationToken);

    public async Task<EmailChannelDto?> GetChannelAsync(int id, CancellationToken cancellationToken = default)
    {
        var channel = await _db.EmailChannels.FindAsync([id], cancellationToken);
        return channel == null ? null : MapChannel(channel);
    }

    public async Task<int> SaveChannelAsync(EmailChannelDto dto, CancellationToken cancellationToken = default)
    {
        ValidateChannel(dto);

        EmailChannel entity;
        if (dto.Id > 0)
        {
            entity = await _db.EmailChannels.FindAsync([dto.Id], cancellationToken)
                ?? throw new NotFoundException($"Email channel {dto.Id} was not found.");
        }
        else
        {
            entity = new EmailChannel();
            _db.EmailChannels.Add(entity);
        }

        if (await _db.EmailChannels.AnyAsync(
            c => c.Code == dto.Code && c.Id != dto.Id, cancellationToken))
            throw new BusinessException($"Email channel code '{dto.Code}' already exists.");

        if (dto.IsDefault)
            await ClearDefaultChannelAsync(dto.Id, cancellationToken);

        MapChannelToEntity(dto, entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task DeleteChannelAsync(int id, CancellationToken cancellationToken = default)
    {
        var channel = await _db.EmailChannels.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Email channel {id} was not found.");

        if (await _db.EmailTemplates.AnyAsync(t => t.EmailChannelId == id, cancellationToken))
            throw new BusinessException("Cannot delete a channel that is referenced by email templates.");

        if (await _db.EmailMessages.AnyAsync(m => m.EmailChannelId == id, cancellationToken))
            throw new BusinessException("Cannot delete a channel that has sent email history.");

        _db.EmailChannels.Remove(channel);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailRetryPolicyListItemDto>> GetRetryPoliciesAsync(CancellationToken cancellationToken = default) =>
        await _db.EmailRetryPolicies
            .OrderBy(p => p.Name)
            .Select(p => new EmailRetryPolicyListItemDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                MaxAttempts = p.MaxAttempts,
                BackoffStrategy = p.BackoffStrategy,
                IsActive = p.IsActive,
                IsDefault = p.IsDefault
            })
            .ToListAsync(cancellationToken);

    public async Task<EmailRetryPolicyDto?> GetRetryPolicyAsync(int id, CancellationToken cancellationToken = default)
    {
        var policy = await _db.EmailRetryPolicies.FindAsync([id], cancellationToken);
        return policy == null ? null : MapPolicy(policy);
    }

    public async Task<int> SaveRetryPolicyAsync(EmailRetryPolicyDto dto, CancellationToken cancellationToken = default)
    {
        ValidatePolicy(dto);

        EmailRetryPolicy entity;
        if (dto.Id > 0)
        {
            entity = await _db.EmailRetryPolicies.FindAsync([dto.Id], cancellationToken)
                ?? throw new NotFoundException($"Retry policy {dto.Id} was not found.");
        }
        else
        {
            entity = new EmailRetryPolicy();
            _db.EmailRetryPolicies.Add(entity);
        }

        if (await _db.EmailRetryPolicies.AnyAsync(
            p => p.Code == dto.Code && p.Id != dto.Id, cancellationToken))
            throw new BusinessException($"Retry policy code '{dto.Code}' already exists.");

        if (dto.IsDefault)
            await ClearDefaultPolicyAsync(dto.Id, cancellationToken);

        MapPolicyToEntity(dto, entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task DeleteRetryPolicyAsync(int id, CancellationToken cancellationToken = default)
    {
        var policy = await _db.EmailRetryPolicies.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Retry policy {id} was not found.");

        if (await _db.EmailTemplates.AnyAsync(t => t.RetryPolicyId == id, cancellationToken))
            throw new BusinessException("Cannot delete a retry policy referenced by email templates.");

        _db.EmailRetryPolicies.Remove(policy);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailTemplateListItemDto>> GetTemplatesAsync(CancellationToken cancellationToken = default) =>
        await _db.EmailTemplates
            .Include(t => t.EmailChannel)
            .OrderBy(t => t.Name)
            .Select(t => new EmailTemplateListItemDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                Description = t.Description,
                ChannelName = t.EmailChannel != null ? t.EmailChannel.Name : null,
                BindingCount = t.Bindings.Count,
                IsActive = t.IsActive
            })
            .ToListAsync(cancellationToken);

    public async Task<EmailTemplateDto?> GetTemplateAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await _db.EmailTemplates
            .Include(t => t.Bindings)
            .ThenInclude(b => b.Form)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return template == null ? null : MapTemplate(template);
    }

    public async Task<int> SaveTemplateAsync(EmailTemplateDto dto, CancellationToken cancellationToken = default)
    {
        ValidateTemplate(dto);

        EmailTemplate entity;
        if (dto.Id > 0)
        {
            entity = await _db.EmailTemplates
                .Include(t => t.Bindings)
                .FirstOrDefaultAsync(t => t.Id == dto.Id, cancellationToken)
                ?? throw new NotFoundException($"Email template {dto.Id} was not found.");
        }
        else
        {
            entity = new EmailTemplate();
            _db.EmailTemplates.Add(entity);
        }

        if (await _db.EmailTemplates.AnyAsync(
            t => t.Code == dto.Code && t.Id != dto.Id, cancellationToken))
            throw new BusinessException($"Email template code '{dto.Code}' already exists.");

        entity.Code = dto.Code.Trim();
        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description?.Trim();
        entity.Subject = dto.Subject;
        entity.BodyHtml = dto.BodyHtml;
        entity.BodyText = dto.BodyText;
        entity.DefaultToExpression = dto.DefaultToExpression?.Trim();
        entity.DefaultCc = dto.DefaultCc?.Trim();
        entity.DefaultBcc = dto.DefaultBcc?.Trim();
        entity.EmailChannelId = dto.EmailChannelId;
        entity.RetryPolicyId = dto.RetryPolicyId;
        entity.Culture = dto.Culture;
        entity.IsActive = dto.IsActive;

        SyncBindings(entity, dto.Bindings);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task DeleteTemplateAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await _db.EmailTemplates
            .Include(t => t.Bindings)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Email template {id} was not found.");

        _db.EmailTemplateBindings.RemoveRange(template.Bindings);
        _db.EmailTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetAvailableTokensAsync(int formId, CancellationToken cancellationToken = default)
    {
        var form = await _db.ForgeForms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == formId, cancellationToken)
            ?? throw new NotFoundException($"Form {formId} was not found.");

        var metadata = _discoveryService.Discover(form.EntityName);
        if (metadata == null)
            return ["Now", "AppName"];

        var tokens = metadata.Properties
            .Where(p => p.Name != "Id" || p.IsKey)
            .Select(p => p.Name)
            .ToList();

        tokens.Add("Now");
        tokens.Add("AppName");
        return tokens;
    }

    public async Task<IReadOnlyList<FormOptionDto>> GetFormOptionsAsync(CancellationToken cancellationToken = default) =>
        await _db.ForgeForms
            .AsNoTracking()
            .Where(f => f.IsActive)
            .OrderBy(f => f.Name)
            .Select(f => new FormOptionDto
            {
                Id = f.Id,
                Code = f.Code,
                Name = f.Name,
                EntityName = f.EntityName
            })
            .ToListAsync(cancellationToken);

    private void SyncBindings(EmailTemplate entity, IReadOnlyList<EmailTemplateBindingDto> bindings)
    {
        var incomingIds = bindings.Where(b => b.Id > 0).Select(b => b.Id).ToHashSet();
        var toRemove = entity.Bindings.Where(b => !incomingIds.Contains(b.Id)).ToList();
        _db.EmailTemplateBindings.RemoveRange(toRemove);

        foreach (var dto in bindings)
        {
            EmailTemplateBinding binding;
            if (dto.Id > 0)
            {
                binding = entity.Bindings.FirstOrDefault(b => b.Id == dto.Id)
                    ?? throw new BusinessException($"Binding {dto.Id} was not found on template.");
            }
            else
            {
                binding = new EmailTemplateBinding();
                entity.Bindings.Add(binding);
            }

            binding.FormId = dto.FormId;
            binding.TriggerEvent = dto.TriggerEvent;
            binding.ActionCode = dto.ActionCode?.Trim();
            binding.RecipientField = dto.RecipientField?.Trim();
            binding.ConditionExpression = dto.ConditionExpression?.Trim();
            binding.IsActive = dto.IsActive;
        }
    }

    private async Task ClearDefaultChannelAsync(int exceptId, CancellationToken cancellationToken)
    {
        var defaults = await _db.EmailChannels
            .Where(c => c.IsDefault && c.Id != exceptId)
            .ToListAsync(cancellationToken);

        foreach (var channel in defaults)
            channel.IsDefault = false;
    }

    private async Task ClearDefaultPolicyAsync(int exceptId, CancellationToken cancellationToken)
    {
        var defaults = await _db.EmailRetryPolicies
            .Where(p => p.IsDefault && p.Id != exceptId)
            .ToListAsync(cancellationToken);

        foreach (var policy in defaults)
            policy.IsDefault = false;
    }

    private static void ValidateChannel(EmailChannelDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            throw new BusinessException("Channel code is required.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessException("Channel name is required.");
        if (string.IsNullOrWhiteSpace(dto.FromAddress))
            throw new BusinessException("From address is required.");
        if (!EmailProviderType.All.Contains(dto.Provider))
            throw new BusinessException($"Provider '{dto.Provider}' is not supported.");
        if (dto.Provider == EmailProviderType.Smtp && string.IsNullOrWhiteSpace(dto.SmtpHost))
            throw new BusinessException("SMTP host is required for SMTP channels.");
    }

    private static void ValidatePolicy(EmailRetryPolicyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            throw new BusinessException("Retry policy code is required.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessException("Retry policy name is required.");
        if (dto.MaxAttempts < 1)
            throw new BusinessException("Max attempts must be at least 1.");
        if (!EmailBackoffStrategy.All.Contains(dto.BackoffStrategy))
            throw new BusinessException($"Backoff strategy '{dto.BackoffStrategy}' is not supported.");
    }

    private static void ValidateTemplate(EmailTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            throw new BusinessException("Template code is required.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessException("Template name is required.");
        if (string.IsNullOrWhiteSpace(dto.Subject))
            throw new BusinessException("Template subject is required.");
        if (string.IsNullOrWhiteSpace(dto.BodyHtml))
            throw new BusinessException("Template body is required.");
    }

    private static EmailChannelDto MapChannel(EmailChannel c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        Provider = c.Provider,
        FromAddress = c.FromAddress,
        FromDisplayName = c.FromDisplayName,
        SmtpHost = c.SmtpHost,
        SmtpPort = c.SmtpPort,
        SmtpUseSsl = c.SmtpUseSsl,
        SmtpUsername = c.SmtpUsername,
        CredentialSecretName = c.CredentialSecretName,
        MaxDegreeOfParallelism = c.MaxDegreeOfParallelism,
        IsActive = c.IsActive,
        IsDefault = c.IsDefault
    };

    private static void MapChannelToEntity(EmailChannelDto dto, EmailChannel entity)
    {
        entity.Code = dto.Code.Trim();
        entity.Name = dto.Name.Trim();
        entity.Provider = dto.Provider;
        entity.FromAddress = dto.FromAddress.Trim();
        entity.FromDisplayName = dto.FromDisplayName?.Trim();
        entity.SmtpHost = dto.SmtpHost?.Trim();
        entity.SmtpPort = dto.SmtpPort;
        entity.SmtpUseSsl = dto.SmtpUseSsl;
        entity.SmtpUsername = dto.SmtpUsername?.Trim();
        entity.CredentialSecretName = dto.CredentialSecretName?.Trim();
        entity.MaxDegreeOfParallelism = dto.MaxDegreeOfParallelism;
        entity.IsActive = dto.IsActive;
        entity.IsDefault = dto.IsDefault;
    }

    private static EmailRetryPolicyDto MapPolicy(EmailRetryPolicy p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Name = p.Name,
        MaxAttempts = p.MaxAttempts,
        BackoffStrategy = p.BackoffStrategy,
        BaseDelaySeconds = p.BaseDelaySeconds,
        MaxDelaySeconds = p.MaxDelaySeconds,
        BackoffMultiplier = p.BackoffMultiplier,
        UseJitter = p.UseJitter,
        IsActive = p.IsActive,
        IsDefault = p.IsDefault
    };

    private static void MapPolicyToEntity(EmailRetryPolicyDto dto, EmailRetryPolicy entity)
    {
        entity.Code = dto.Code.Trim();
        entity.Name = dto.Name.Trim();
        entity.MaxAttempts = dto.MaxAttempts;
        entity.BackoffStrategy = dto.BackoffStrategy;
        entity.BaseDelaySeconds = dto.BaseDelaySeconds;
        entity.MaxDelaySeconds = dto.MaxDelaySeconds;
        entity.BackoffMultiplier = dto.BackoffMultiplier;
        entity.UseJitter = dto.UseJitter;
        entity.IsActive = dto.IsActive;
        entity.IsDefault = dto.IsDefault;
    }

    private static EmailTemplateDto MapTemplate(EmailTemplate t) => new()
    {
        Id = t.Id,
        Code = t.Code,
        Name = t.Name,
        Description = t.Description,
        Subject = t.Subject,
        BodyHtml = t.BodyHtml,
        BodyText = t.BodyText,
        DefaultToExpression = t.DefaultToExpression,
        DefaultCc = t.DefaultCc,
        DefaultBcc = t.DefaultBcc,
        EmailChannelId = t.EmailChannelId,
        RetryPolicyId = t.RetryPolicyId,
        Culture = t.Culture,
        IsActive = t.IsActive,
        Bindings = t.Bindings.Select(b => new EmailTemplateBindingDto
        {
            Id = b.Id,
            FormId = b.FormId,
            FormCode = b.Form?.Code,
            FormName = b.Form?.Name,
            TriggerEvent = b.TriggerEvent,
            ActionCode = b.ActionCode,
            RecipientField = b.RecipientField,
            ConditionExpression = b.ConditionExpression,
            IsActive = b.IsActive
        }).ToList()
    };
}

namespace MetaForge.Infrastructure.Email.Providers;

public interface IEmailProvider
{
    string ProviderCode { get; }

    Task SendAsync(EmailSendContext context, CancellationToken cancellationToken);
}

public sealed record EmailSendContext(
    EmailMessage Message,
    EmailChannel Channel,
    string Credential);

public interface IEmailProviderFactory
{
    IEmailProvider Resolve(string providerCode);
}

public sealed class EmailProviderFactory : IEmailProviderFactory
{
    private readonly IReadOnlyDictionary<string, IEmailProvider> _providers;

    public EmailProviderFactory(IEnumerable<IEmailProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.ProviderCode, StringComparer.OrdinalIgnoreCase);
    }

    public IEmailProvider Resolve(string providerCode) =>
        _providers.TryGetValue(providerCode, out var provider)
            ? provider
            : throw new BusinessException($"Email provider '{providerCode}' is not registered.");
}

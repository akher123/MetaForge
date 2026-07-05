using MetaForge.Application.Configuration;
using MetaForge.Infrastructure.Email.Providers;
using Microsoft.Extensions.Options;

namespace MetaForge.Infrastructure.Email;

public interface IEmailCredentialResolver
{
    string Resolve(EmailChannel channel);
}

public sealed class EmailCredentialResolver : IEmailCredentialResolver
{
    private readonly EmailOptions _options;

    public EmailCredentialResolver(IOptions<EmailOptions> options) => _options = options.Value;

    public string Resolve(EmailChannel channel)
    {
        if (string.IsNullOrWhiteSpace(channel.CredentialSecretName))
            return string.Empty;

        if (_options.Secrets.TryGetValue(channel.CredentialSecretName, out var secret))
            return secret;

        throw new BusinessException(
            $"Email secret '{channel.CredentialSecretName}' was not found in Email:Secrets configuration.");
    }
}

public sealed class EmailChannelSender : IEmailChannelSender
{
    private readonly IEmailProviderFactory _factory;
    private readonly IEmailCredentialResolver _credentialResolver;
    private readonly EmailOptions _options;

    public EmailChannelSender(
        IEmailProviderFactory factory,
        IEmailCredentialResolver credentialResolver,
        IOptions<EmailOptions> options)
    {
        _factory = factory;
        _credentialResolver = credentialResolver;
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, EmailChannel channel, CancellationToken cancellationToken = default)
    {
        if (!_options.SendingEnabled)
            throw new BusinessException("Email sending is disabled in configuration.");

        var provider = _factory.Resolve(channel.Provider);
        var credential = _credentialResolver.Resolve(channel);
        await provider.SendAsync(new Providers.EmailSendContext(message, channel, credential), cancellationToken);
    }
}

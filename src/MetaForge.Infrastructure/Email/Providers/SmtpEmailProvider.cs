using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace MetaForge.Infrastructure.Email.Providers;

/// <summary>
/// Sends email via SMTP using MailKit.
/// </summary>
public sealed class SmtpEmailProvider : IEmailProvider
{
    public string ProviderCode => EmailProviderType.Smtp;

    public async Task SendAsync(EmailSendContext context, CancellationToken cancellationToken)
    {
        var channel = context.Channel;
        if (string.IsNullOrWhiteSpace(channel.SmtpHost))
            throw new BusinessException($"SMTP host is required for channel '{channel.Code}'.");

        using var client = new SmtpClient();
        var secureSocketOptions = channel.SmtpUseSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(channel.SmtpHost, channel.SmtpPort, secureSocketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(channel.SmtpUsername))
            await client.AuthenticateAsync(channel.SmtpUsername, context.Credential, cancellationToken);

        var message = BuildMimeMessage(context);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static MimeMessage BuildMimeMessage(EmailSendContext context)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            context.Channel.FromDisplayName ?? context.Channel.FromAddress,
            context.Channel.FromAddress));
        message.To.Add(MailboxAddress.Parse(context.Message.ToAddress));
        message.Subject = context.Message.Subject;

        AddAddresses(message.Cc, context.Message.Cc);
        AddAddresses(message.Bcc, context.Message.Bcc);

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = context.Message.BodyHtml,
            TextBody = context.Message.BodyText
        };
        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private static void AddAddresses(InternetAddressList list, string? addresses)
    {
        if (string.IsNullOrWhiteSpace(addresses))
            return;

        foreach (var address in addresses.Split(';', ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            list.Add(MailboxAddress.Parse(address));
    }
}

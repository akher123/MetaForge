using SendGrid;
using SendGrid.Helpers.Mail;

namespace MetaForge.Infrastructure.Email.Providers;

/// <summary>
/// Sends email via SendGrid API.
/// </summary>
public sealed class SendGridEmailProvider : IEmailProvider
{
    public string ProviderCode => EmailProviderType.SendGrid;

    public async Task SendAsync(EmailSendContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.Credential))
            throw new BusinessException($"SendGrid API key is required for channel '{context.Channel.Code}'.");

        var client = new SendGridClient(context.Credential);
        var from = new EmailAddress(context.Channel.FromAddress, context.Channel.FromDisplayName);
        var msg = MailHelper.CreateSingleEmail(
            from,
            new EmailAddress(context.Message.ToAddress),
            context.Message.Subject,
            context.Message.BodyText ?? StripHtml(context.Message.BodyHtml),
            context.Message.BodyHtml);

        AddRecipients(msg, context.Message.Cc, (m, a) => m.AddCc(a));
        AddRecipients(msg, context.Message.Bcc, (m, a) => m.AddBcc(a));

        var response = await client.SendEmailAsync(msg, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"SendGrid failed ({response.StatusCode}): {body}");
        }
    }

    private static void AddRecipients(SendGridMessage msg, string? addresses, Action<SendGridMessage, EmailAddress> add)
    {
        if (string.IsNullOrWhiteSpace(addresses))
            return;

        foreach (var address in addresses.Split(';', ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            add(msg, new EmailAddress(address));
    }

    private static string StripHtml(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();
}

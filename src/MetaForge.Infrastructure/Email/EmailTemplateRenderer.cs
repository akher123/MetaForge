using System.Text.RegularExpressions;

namespace MetaForge.Infrastructure.Email;

public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private static readonly Regex TokenPattern = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public RenderedEmail Render(EmailTemplate template, IReadOnlyDictionary<string, object?> tokens)
    {
        var to = RenderToken(template.DefaultToExpression ?? string.Empty, tokens);
        if (string.IsNullOrWhiteSpace(to))
            throw new BusinessException("Email template does not resolve a recipient address.");

        return new RenderedEmail
        {
            ToAddress = to.Trim(),
            Cc = RenderOptional(template.DefaultCc, tokens),
            Bcc = RenderOptional(template.DefaultBcc, tokens),
            Subject = RenderToken(template.Subject, tokens),
            BodyHtml = RenderToken(template.BodyHtml, tokens),
            BodyText = string.IsNullOrWhiteSpace(template.BodyText)
                ? null
                : RenderToken(template.BodyText, tokens)
        };
    }

    public string RenderToken(string template, IReadOnlyDictionary<string, object?> tokens)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        return TokenPattern.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!tokens.TryGetValue(key, out var value) || value == null)
                return string.Empty;

            return value switch
            {
                DateTime dt => dt.ToString("g"),
                DateTimeOffset dto => dto.ToString("g"),
                bool b => b ? "Yes" : "No",
                _ => value.ToString() ?? string.Empty
            };
        });
    }

    private string? RenderOptional(string? template, IReadOnlyDictionary<string, object?> tokens)
    {
        if (string.IsNullOrWhiteSpace(template))
            return null;

        var rendered = RenderToken(template, tokens);
        return string.IsNullOrWhiteSpace(rendered) ? null : rendered;
    }
}

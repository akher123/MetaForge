using MetaForge.Domain.Notifications;
using MetaForge.Infrastructure.Email;

namespace MetaForge.UnitTests;

public class EmailTemplateRendererTests
{
    private readonly EmailTemplateRenderer _renderer = new();

    [Fact]
    public void Render_ReplacesTokensInSubjectAndBody()
    {
        var template = new EmailTemplate
        {
            Subject = "Hello {{Name}}",
            BodyHtml = "<p>Welcome {{Name}}, your email is {{Email}}</p>",
            DefaultToExpression = "{{Email}}"
        };

        var tokens = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@example.com"
        };

        var result = _renderer.Render(template, tokens);

        Assert.Equal("Hello Alice", result.Subject);
        Assert.Equal("<p>Welcome Alice, your email is alice@example.com</p>", result.BodyHtml);
        Assert.Equal("alice@example.com", result.ToAddress);
    }

    [Fact]
    public void Render_ThrowsWhenRecipientMissing()
    {
        var template = new EmailTemplate
        {
            Subject = "Test",
            BodyHtml = "Body",
            DefaultToExpression = "{{MissingEmail}}"
        };

        Assert.Throws<MetaForge.Shared.Exceptions.BusinessException>(() =>
            _renderer.Render(template, new Dictionary<string, object?>()));
    }
}

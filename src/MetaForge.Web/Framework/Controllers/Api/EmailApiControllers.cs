namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/emailconfig")]
public class EmailConfigApiController : ControllerBase
{
    private readonly IEmailConfigurationService _configService;

    public EmailConfigApiController(IEmailConfigurationService configService) => _configService = configService;

    [HttpGet("channels")]
    [RequirePermissionCode(EmailConfigPermissions.View)]
    public async Task<IActionResult> GetChannels(CancellationToken cancellationToken) =>
        Ok(await _configService.GetChannelsAsync(cancellationToken));

    [HttpGet("channels/{id:int}")]
    [RequirePermissionCode(EmailConfigPermissions.View)]
    public async Task<IActionResult> GetChannel(int id, CancellationToken cancellationToken)
    {
        var channel = await _configService.GetChannelAsync(id, cancellationToken);
        return channel == null ? NotFound() : Ok(channel);
    }

    [HttpPost("channels")]
    [RequirePermissionCode(EmailConfigPermissions.Manage)]
    public async Task<IActionResult> SaveChannel([FromBody] EmailChannelDto dto, CancellationToken cancellationToken)
    {
        var id = await _configService.SaveChannelAsync(dto, cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("channels/{id:int}")]
    [RequirePermissionCode(EmailConfigPermissions.Manage)]
    public async Task<IActionResult> DeleteChannel(int id, CancellationToken cancellationToken)
    {
        await _configService.DeleteChannelAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("retry-policies")]
    [RequirePermissionCode(EmailConfigPermissions.View)]
    public async Task<IActionResult> GetRetryPolicies(CancellationToken cancellationToken) =>
        Ok(await _configService.GetRetryPoliciesAsync(cancellationToken));

    [HttpGet("retry-policies/{id:int}")]
    [RequirePermissionCode(EmailConfigPermissions.View)]
    public async Task<IActionResult> GetRetryPolicy(int id, CancellationToken cancellationToken)
    {
        var policy = await _configService.GetRetryPolicyAsync(id, cancellationToken);
        return policy == null ? NotFound() : Ok(policy);
    }

    [HttpPost("retry-policies")]
    [RequirePermissionCode(EmailConfigPermissions.Manage)]
    public async Task<IActionResult> SaveRetryPolicy([FromBody] EmailRetryPolicyDto dto, CancellationToken cancellationToken)
    {
        var id = await _configService.SaveRetryPolicyAsync(dto, cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("retry-policies/{id:int}")]
    [RequirePermissionCode(EmailConfigPermissions.Manage)]
    public async Task<IActionResult> DeleteRetryPolicy(int id, CancellationToken cancellationToken)
    {
        await _configService.DeleteRetryPolicyAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("templates")]
    [RequirePermissionCode(EmailConfigPermissions.View)]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken) =>
        Ok(await _configService.GetTemplatesAsync(cancellationToken));

    [HttpGet("templates/{id:int}")]
    [RequirePermissionCode(EmailConfigPermissions.View)]
    public async Task<IActionResult> GetTemplate(int id, CancellationToken cancellationToken)
    {
        var template = await _configService.GetTemplateAsync(id, cancellationToken);
        return template == null ? NotFound() : Ok(template);
    }

    [HttpPost("templates")]
    [RequirePermissionCode(EmailConfigPermissions.Manage)]
    public async Task<IActionResult> SaveTemplate([FromBody] EmailTemplateDto dto, CancellationToken cancellationToken)
    {
        var id = await _configService.SaveTemplateAsync(dto, cancellationToken);
        return Ok(new { id, url = $"/EmailAdmin/Templates/Edit/{id}" });
    }

    [HttpDelete("templates/{id:int}")]
    [RequirePermissionCode(EmailConfigPermissions.Manage)]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken cancellationToken)
    {
        await _configService.DeleteTemplateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("forms")]
    [RequirePermissionCode(EmailConfigPermissions.View)]
    public async Task<IActionResult> GetForms(CancellationToken cancellationToken) =>
        Ok(await _configService.GetFormOptionsAsync(cancellationToken));

    [HttpGet("tokens/{formId:int}")]
    [RequirePermissionCode(EmailConfigPermissions.View)]
    public async Task<IActionResult> GetTokens(int formId, CancellationToken cancellationToken) =>
        Ok(await _configService.GetAvailableTokensAsync(formId, cancellationToken));
}

[Authorize]
[ApiController]
[Route("api/metaforge/email")]
public class EmailApiController : ControllerBase
{
    private readonly IEmailDispatchService _dispatchService;
    private readonly IEmailMessageService _messageService;

    public EmailApiController(
        IEmailDispatchService dispatchService,
        IEmailMessageService messageService)
    {
        _dispatchService = dispatchService;
        _messageService = messageService;
    }

    [HttpPost("send")]
    [RequirePermissionCode(EmailConfigPermissions.Manage)]
    public async Task<IActionResult> Send([FromBody] EmailSendRequest request, CancellationToken cancellationToken)
    {
        var id = await _dispatchService.EnqueueFromTemplateAsync(request, cancellationToken);
        return Ok(new { messageId = id, status = "Queued" });
    }

    [HttpGet("messages")]
    [RequirePermissionCode(EmailConfigPermissions.View)]
    public async Task<IActionResult> GetMessages([FromQuery] EmailLogQuery query, CancellationToken cancellationToken) =>
        Ok(await _messageService.GetMessagesAsync(query, cancellationToken));

    [HttpPost("messages/{id:int}/cancel")]
    [RequirePermissionCode(EmailConfigPermissions.Manage)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await _messageService.CancelAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("messages/{id:int}/resend")]
    [RequirePermissionCode(EmailConfigPermissions.Manage)]
    public async Task<IActionResult> Resend(int id, CancellationToken cancellationToken)
    {
        var newId = await _messageService.ResendAsync(id, cancellationToken);
        return Ok(new { messageId = newId, status = "Queued" });
    }
}

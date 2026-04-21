using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Authorization;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/ai-databases")]
[Authorize]
public sealed class AiDatabasesController : ControllerBase
{
    private const string ResourceType = "database";

    private readonly IAiDatabaseService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAuditWriter _auditWriter;
    private readonly IResourceWriteGate _writeGate;
    private readonly IValidator<AiDatabaseCreateRequest> _createValidator;
    private readonly IValidator<AiDatabaseUpdateRequest> _updateValidator;
    private readonly IValidator<AiDatabaseRecordCreateRequest> _recordCreateValidator;
    private readonly IValidator<AiDatabaseRecordUpdateRequest> _recordUpdateValidator;
    private readonly IValidator<AiDatabaseRecordBulkCreateRequest> _recordBulkValidator;
    private readonly IValidator<AiDatabaseSchemaValidateRequest> _schemaValidator;
    private readonly IValidator<AiDatabaseImportRequest> _importValidator;

    public AiDatabasesController(
        IAiDatabaseService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IAuditWriter auditWriter,
        IResourceWriteGate writeGate,
        IValidator<AiDatabaseCreateRequest> createValidator,
        IValidator<AiDatabaseUpdateRequest> updateValidator,
        IValidator<AiDatabaseRecordCreateRequest> recordCreateValidator,
        IValidator<AiDatabaseRecordUpdateRequest> recordUpdateValidator,
        IValidator<AiDatabaseRecordBulkCreateRequest> recordBulkValidator,
        IValidator<AiDatabaseSchemaValidateRequest> schemaValidator,
        IValidator<AiDatabaseImportRequest> importValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _auditWriter = auditWriter;
        _writeGate = writeGate;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _recordCreateValidator = recordCreateValidator;
        _recordUpdateValidator = recordUpdateValidator;
        _recordBulkValidator = recordBulkValidator;
        _schemaValidator = schemaValidator;
        _importValidator = importValidator;
    }

    private async Task WriteDatabaseAuditAsync(string action, string target, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUserOrThrow();
        await _auditWriter.WriteAsync(
            new AuditRecord(tenantId, user.UserId.ToString(), action, "success", target, null, null),
            cancellationToken);
    }

    /// <summary>D9：从当前 HTTP 上下文解析行级 owner/channel；用作 SingleUser/Channel 模式的策略输入。</summary>
    private (long? OwnerUserId, long? CreatorUserId, string? ChannelId) ResolveRowMetadata()
    {
        var user = _currentUserAccessor.GetCurrentUserOrThrow();
        var userId = user.UserId;
        var channelId = HttpContext.Request.Headers.TryGetValue("X-App-Channel", out var chVals)
            ? chVals.ToString()
            : null;
        return (userId, userId, string.IsNullOrWhiteSpace(channelId) ? null : channelId);
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiDatabaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiDatabaseListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        [FromQuery] long? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetPagedAsync(tenantId, keyword, workspaceId, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AiDatabaseListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseView)]
    public async Task<ActionResult<ApiResponse<AiDatabaseDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiDatabaseDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AiDatabaseNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiDatabaseDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiDatabaseCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AiDatabaseCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateAsync(tenantId, request, cancellationToken);
        await WriteDatabaseAuditAsync("ai_database.create", $"db:{id}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AiDatabaseUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "edit", cancellationToken);
        await _service.UpdateAsync(tenantId, id, request, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        await WriteDatabaseAuditAsync("ai_database.update", $"db:{id}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _writeGate.GuardByResourceAsync(tenantId, ResourceType, id, "delete", cancellationToken);
        await _service.DeleteAsync(tenantId, id, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        await WriteDatabaseAuditAsync("ai_database.delete", $"db:{id}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/bind-bot")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> BindBot(
        long id,
        [FromBody] AiDatabaseBindBotRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BotId <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ValidationError, ApiResponseLocalizer.T(HttpContext, "AiDatabaseBotIdInvalid"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _service.BindAsync(tenantId, id, request.BotId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString(), request.BotId }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/bind-bot")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UnbindBot(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UnbindAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/records")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiDatabaseRecordListItem>>>> GetRecords(
        long id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetRecordsAsync(tenantId, id, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AiDatabaseRecordListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/records")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseCreate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateRecord(
        long id,
        [FromBody] AiDatabaseRecordCreateRequest request,
        CancellationToken cancellationToken)
    {
        _recordCreateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var (ownerUserId, creatorUserId, channelId) = ResolveRowMetadata();
        var recordId = await _service.CreateRecordAsync(tenantId, id, request, cancellationToken, ownerUserId, creatorUserId, channelId);
        await WriteDatabaseAuditAsync("ai_database_record.create", $"db:{id}/record:{recordId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = recordId.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>D5：同步批量插入。受 <c>AiDatabaseQuota.MaxBulkInsertRows</c> 限制（默认 1000 行）。</summary>
    [HttpPost("{id:long}/records/bulk")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseCreate)]
    public async Task<ActionResult<ApiResponse<AiDatabaseRecordBulkCreateResult>>> CreateRecordsBulk(
        long id,
        [FromBody] AiDatabaseRecordBulkCreateRequest request,
        CancellationToken cancellationToken)
    {
        _recordBulkValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var (ownerUserId, creatorUserId, channelId) = ResolveRowMetadata();
        var result = await _service.CreateRecordsBulkAsync(tenantId, id, request, cancellationToken, ownerUserId, creatorUserId, channelId);
        await WriteDatabaseAuditAsync(
            "ai_database_record.bulk_create",
            $"db:{id}/total:{result.Total}/ok:{result.Succeeded}/fail:{result.Failed}",
            cancellationToken);
        return Ok(ApiResponse<AiDatabaseRecordBulkCreateResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>D5：异步批量插入；返回 taskId，进度通过 <c>imports/latest</c> 查询。</summary>
    [HttpPost("{id:long}/records/bulk-async")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseCreate)]
    public async Task<ActionResult<ApiResponse<AiDatabaseBulkJobAccepted>>> SubmitBulkInsertJob(
        long id,
        [FromBody] AiDatabaseRecordBulkCreateRequest request,
        CancellationToken cancellationToken)
    {
        _recordBulkValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var (ownerUserId, creatorUserId, channelId) = ResolveRowMetadata();
        var accepted = await _service.SubmitBulkInsertJobAsync(tenantId, id, request, cancellationToken, ownerUserId, creatorUserId, channelId);
        await WriteDatabaseAuditAsync(
            "ai_database_record.bulk_async.submit",
            $"db:{id}/task:{accepted.TaskId}/rows:{accepted.RowCount}",
            cancellationToken);
        return Accepted(ApiResponse<AiDatabaseBulkJobAccepted>.Ok(accepted, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/records/{recordId:long}")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRecord(
        long id,
        long recordId,
        [FromBody] AiDatabaseRecordUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _recordUpdateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateRecordAsync(tenantId, id, recordId, request, cancellationToken);
        await WriteDatabaseAuditAsync("ai_database_record.update", $"db:{id}/record:{recordId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = recordId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/records/{recordId:long}")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRecord(
        long id,
        long recordId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteRecordAsync(tenantId, id, recordId, cancellationToken);
        await WriteDatabaseAuditAsync("ai_database_record.delete", $"db:{id}/record:{recordId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = recordId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/schema")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseView)]
    public async Task<ActionResult<ApiResponse<object>>> GetSchema(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var schema = await _service.GetSchemaAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { tableSchema = schema }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/schema/validate")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseUpdate)]
    public async Task<ActionResult<ApiResponse<AiDatabaseSchemaValidateResult>>> ValidateSchema(
        long id,
        [FromBody] AiDatabaseSchemaValidateRequest request,
        CancellationToken cancellationToken)
    {
        _schemaValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.GetSchemaAsync(tenantId, id, cancellationToken);
        var result = await _service.ValidateSchemaAsync(request.TableSchema, cancellationToken);
        return Ok(ApiResponse<AiDatabaseSchemaValidateResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("schema-validations")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseCreate)]
    public async Task<ActionResult<ApiResponse<AiDatabaseSchemaValidateResult>>> ValidateSchemaForCreate(
        [FromBody] AiDatabaseSchemaValidateRequest request,
        CancellationToken cancellationToken)
    {
        _schemaValidator.ValidateAndThrow(request);
        var result = await _service.ValidateSchemaAsync(request.TableSchema, cancellationToken);
        return Ok(ApiResponse<AiDatabaseSchemaValidateResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/imports")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseCreate)]
    public async Task<ActionResult<ApiResponse<object>>> SubmitImport(
        long id,
        [FromBody] AiDatabaseImportRequest request,
        CancellationToken cancellationToken)
    {
        _importValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var (ownerUserId, creatorUserId, channelId) = ResolveRowMetadata();
        var taskId = await _service.SubmitImportAsync(tenantId, id, request, cancellationToken, ownerUserId, creatorUserId, channelId);
        await WriteDatabaseAuditAsync("ai_database_record.import.submit", $"db:{id}/task:{taskId}/file:{request.FileId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TaskId = taskId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/imports/latest")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseView)]
    public async Task<ActionResult<ApiResponse<AiDatabaseImportProgress>>> GetImportProgress(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetImportProgressAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiDatabaseImportProgress>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "AiDatabaseImportTaskNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiDatabaseImportProgress>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/template")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseView)]
    public async Task<IActionResult> DownloadTemplate(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var template = await _service.GetTemplateAsync(tenantId, id, cancellationToken);
        return File(template.Content, template.ContentType, template.FileName);
    }

    public sealed record AiDatabaseBindBotRequest(long BotId);
}

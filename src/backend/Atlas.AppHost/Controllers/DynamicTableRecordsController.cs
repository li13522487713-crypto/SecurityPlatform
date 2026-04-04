using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/dynamic-tables/{tableKey}/records")]
public sealed class DynamicTableRecordsController : ControllerBase
{
    private readonly IDynamicRecordQueryService _queryService;
    private readonly IDynamicRecordCommandService _commandService;
    private readonly IDynamicTableCommandService _tableCommandService;
    private readonly IDynamicRecordImportService _importService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IValidator<DynamicRecordUpsertRequest> _upsertValidator;
    private readonly IValidator<DynamicRecordQueryRequest> _queryValidator;
    private readonly IValidator<DynamicRecordExportRequest> _exportValidator;

    public DynamicTableRecordsController(
        IDynamicRecordQueryService queryService,
        IDynamicRecordCommandService commandService,
        IDynamicTableCommandService tableCommandService,
        IDynamicRecordImportService importService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder,
        IValidator<DynamicRecordUpsertRequest> upsertValidator,
        IValidator<DynamicRecordQueryRequest> queryValidator,
        IValidator<DynamicRecordExportRequest> exportValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tableCommandService = tableCommandService;
        _importService = importService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
        _upsertValidator = upsertValidator;
        _queryValidator = queryValidator;
        _exportValidator = exportValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<DynamicRecordListResult>>> Query(
        string tableKey,
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        CancellationToken cancellationToken = default)
    {
        var queryRequest = new DynamicRecordQueryRequest(
            request.PageIndex,
            request.PageSize,
            keyword,
            sortBy,
            sortDesc,
            Array.Empty<DynamicFilterCondition>());
        _queryValidator.ValidateAndThrow(queryRequest);

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, tableKey, queryRequest, cancellationToken);
        return Ok(ApiResponse<DynamicRecordListResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("query")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<DynamicRecordListResult>>> QueryWithBody(
        string tableKey,
        [FromBody] DynamicRecordQueryRequest request,
        CancellationToken cancellationToken)
    {
        _queryValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, tableKey, request, cancellationToken);
        return Ok(ApiResponse<DynamicRecordListResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<DynamicRecordDto?>>> GetById(
        string tableKey,
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var record = await _queryService.GetByIdAsync(tenantId, tableKey, id, cancellationToken);
        return Ok(ApiResponse<DynamicRecordDto?>.Ok(record, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        string tableKey,
        [FromBody] DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        _upsertValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        await RecordAuditAsync(currentUser, "CREATE_DYNAMIC_RECORD", $"{tableKey}:{id}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        string tableKey,
        long id,
        [FromBody] DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        _upsertValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, currentUser.UserId, tableKey, id, request, cancellationToken);
        await RecordAuditAsync(currentUser, "UPDATE_DYNAMIC_RECORD", $"{tableKey}:{id}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string tableKey,
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, currentUser.UserId, tableKey, id, cancellationToken);
        await RecordAuditAsync(currentUser, "DELETE_DYNAMIC_RECORD", $"{tableKey}:{id}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteBatch(
        string tableKey,
        [FromBody] DynamicRecordBatchDeleteRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteBatchAsync(tenantId, currentUser.UserId, tableKey, request.Ids, cancellationToken);
        await RecordAuditAsync(currentUser, "DELETE_BATCH_DYNAMIC_RECORD", $"{tableKey}:{string.Join(',', request.Ids)}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Count = request.Ids.Count }, HttpContext.TraceIdentifier));
    }

    [HttpPost("export")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<IActionResult> Export(
        string tableKey,
        [FromBody] DynamicRecordExportRequest request,
        CancellationToken cancellationToken)
    {
        _exportValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var fileName = $"{tableKey}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        await _queryService.WriteCsvAsync(
            tenantId,
            tableKey,
            request,
            Response.Body,
            cancellationToken);
        return new EmptyResult();
    }

    [HttpPost("import")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<DynamicRecordImportResult>>> Import(
        string tableKey,
        [FromBody] DynamicRecordImportRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<DynamicRecordImportResult>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _importService.ImportAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        await RecordAuditAsync(currentUser, "IMPORT_DYNAMIC_RECORDS", $"{tableKey}:{result.ImportedRows}", cancellationToken);
        return Ok(ApiResponse<DynamicRecordImportResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("import/analyze")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<DynamicRecordImportAnalyzeResult>>> AnalyzeImport(
        string tableKey,
        [FromForm] IFormFile file,
        [FromForm] string format,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<DynamicRecordImportAnalyzeResult>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        if (file is null || file.Length <= 0)
        {
            return BadRequest(ApiResponse<DynamicRecordImportAnalyzeResult>.Fail(
                ErrorCodes.ValidationError,
                "Import file is required.",
                HttpContext.TraceIdentifier));
        }

        string content;
        var normalizedFormat = (format ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedFormat is "xlsx" or "xls")
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            content = Convert.ToBase64String(ms.ToArray());
            normalizedFormat = "xlsx";
        }
        else
        {
            using var reader = new StreamReader(file.OpenReadStream());
            content = await reader.ReadToEndAsync();
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _importService.AnalyzeAsync(
            tenantId,
            currentUser.UserId,
            tableKey,
            new DynamicRecordImportRequest(normalizedFormat, content, true),
            cancellationToken);
        await RecordAuditAsync(currentUser, "ANALYZE_DYNAMIC_RECORD_IMPORT", $"{tableKey}:{result.SessionId}", cancellationToken);
        return Ok(ApiResponse<DynamicRecordImportAnalyzeResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("import/commit")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<DynamicRecordImportResult>>> CommitImport(
        string tableKey,
        [FromBody] DynamicRecordImportCommitRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<DynamicRecordImportResult>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _importService.CommitAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        await RecordAuditAsync(currentUser, "COMMIT_DYNAMIC_RECORD_IMPORT", $"{tableKey}:{result.ImportedRows}", cancellationToken);
        return Ok(ApiResponse<DynamicRecordImportResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("excel-paste")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<DynamicRecordImportResult>>> PasteFromExcel(
        string tableKey,
        [FromBody] DynamicRecordImportRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<DynamicRecordImportResult>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _importService.PasteFromExcelAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        await RecordAuditAsync(currentUser, "PASTE_EXCEL_DYNAMIC_RECORDS", $"{tableKey}:{result.ImportedRows}", cancellationToken);
        return Ok(ApiResponse<DynamicRecordImportResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 从动态表记录发起审批
    /// </summary>
    [HttpPost("{recordId:long}/approval")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<DynamicTableApprovalSubmitResponse>>> SubmitApproval(
        string tableKey,
        long recordId,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<DynamicTableApprovalSubmitResponse>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _tableCommandService.SubmitApprovalAsync(
            tenantId, currentUser.UserId, tableKey, recordId, cancellationToken);
        await RecordAuditAsync(currentUser, "SUBMIT_DYNAMIC_RECORD_APPROVAL", $"{tableKey}:{recordId}", cancellationToken);
        return Ok(ApiResponse<DynamicTableApprovalSubmitResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Query related records for master-detail scenarios via DynamicRelation.
    /// </summary>
    [HttpGet("{id:long}/related/{relatedTableKey}")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<DynamicRecordListResult>>> GetRelatedRecords(
        string tableKey,
        long id,
        string relatedTableKey,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetRelatedRecordsAsync(
            tenantId, tableKey, id, relatedTableKey, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<DynamicRecordListResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    private Task RecordAuditAsync(
        CurrentUserInfo currentUser,
        string action,
        string target,
        CancellationToken cancellationToken)
    {
        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;
        var context = new AuditContext(
            currentUser.TenantId,
            actor,
            action,
            "SUCCESS",
            target,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        return _auditRecorder.RecordAsync(context, cancellationToken);
    }
}

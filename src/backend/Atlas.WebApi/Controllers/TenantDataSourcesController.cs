using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 租户数据源管理（等保2.0 数据隔离）
/// </summary>
[ApiController]
[Route("api/v1/tenant-datasources")]
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class TenantDataSourcesController : ControllerBase
{
    private readonly TenantDataSourceRepository _repository;
    private readonly ITenantDbConnectionFactory _connectionFactory;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;
    private readonly Atlas.Infrastructure.Options.DatabaseEncryptionOptions _encryptionOptions;

    public TenantDataSourcesController(
        TenantDataSourceRepository repository,
        ITenantDbConnectionFactory connectionFactory,
        IIdGeneratorAccessor idGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder,
        IOptions<Atlas.Infrastructure.Options.DatabaseEncryptionOptions> encryptionOptions)
    {
        _repository = repository;
        _connectionFactory = connectionFactory;
        _idGenerator = idGenerator;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
        _encryptionOptions = encryptionOptions.Value;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TenantDataSourceDto>>>> GetAll(CancellationToken ct = default)
    {
        var sources = await _repository.QueryAllAsync(ct);
        var dtos = sources.Select(s => new TenantDataSourceDto(
            s.Id.ToString(),
            s.TenantIdValue,
            s.Name,
            s.DbType,
            s.IsActive,
            s.CreatedAt,
            s.UpdatedAt)).ToList();
        return Ok(ApiResponse<List<TenantDataSourceDto>>.Ok(dtos, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] TenantDataSourceCreateRequest request,
        CancellationToken ct = default)
    {
        var encrypted = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Encrypt(request.ConnectionString, _encryptionOptions.Key)
            : request.ConnectionString;

        var entity = new TenantDataSource(
            request.TenantIdValue, request.Name, encrypted, request.DbType, _idGenerator.NextId());
        await _repository.AddAsync(entity, ct);
        _connectionFactory.InvalidateCache(request.TenantIdValue);

        await RecordAuditAsync("CREATE_DATASOURCE", request.TenantIdValue, ct);
        return Ok(ApiResponse<object>.Ok(new { Id = entity.Id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] TenantDataSourceUpdateRequest request,
        CancellationToken ct = default)
    {
        var entity = await _repository.FindByIdAsync(id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "数据源不存在", HttpContext.TraceIdentifier));

        var encrypted = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Encrypt(request.ConnectionString, _encryptionOptions.Key)
            : request.ConnectionString;

        entity.Update(request.Name, encrypted, request.DbType);
        await _repository.UpdateAsync(entity, ct);
        _connectionFactory.InvalidateCache(entity.TenantIdValue);

        await RecordAuditAsync("UPDATE_DATASOURCE", id.ToString(), ct);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken ct = default)
    {
        var entity = await _repository.FindByIdAsync(id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "数据源不存在", HttpContext.TraceIdentifier));

        await _repository.DeleteAsync(id, ct);
        _connectionFactory.InvalidateCache(entity.TenantIdValue);

        await RecordAuditAsync("DELETE_DATASOURCE", id.ToString(), ct);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("test")]
    public ActionResult<ApiResponse<TestConnectionResult>> TestConnection(
        [FromBody] TestConnectionRequest request)
    {
        try
        {
            var dbType = request.DbType.Equals("SQLite", StringComparison.OrdinalIgnoreCase)
                ? DbType.Sqlite
                : DbType.SqlServer;

            var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = request.ConnectionString,
                DbType = dbType,
                IsAutoCloseConnection = true
            });
            db.Ado.CheckConnection();
            return Ok(ApiResponse<TestConnectionResult>.Ok(
                new TestConnectionResult(true), HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<TestConnectionResult>.Ok(
                new TestConnectionResult(false, ex.Message), HttpContext.TraceIdentifier));
        }
    }

    private async Task RecordAuditAsync(string action, string target, CancellationToken ct)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null) return;
        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;
        var auditContext = new AuditContext(
            currentUser.TenantId, actor, action, "SUCCESS", target,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, ct);
    }
}

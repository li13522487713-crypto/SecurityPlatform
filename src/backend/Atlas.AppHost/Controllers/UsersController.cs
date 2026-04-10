using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Authorization;
using ClosedXML.Excel;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserQueryService _userQueryService;
    private readonly IUserCommandService _userCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IValidator<UserCreateRequest> _createValidator;
    private readonly IValidator<UserUpdateRequest> _updateValidator;
    private readonly IExcelExportService _excelExportService;

    public UsersController(
        IUserQueryService userQueryService,
        IUserCommandService userCommandService,
        ITenantProvider tenantProvider,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor,
        IValidator<UserCreateRequest> createValidator,
        IValidator<UserUpdateRequest> updateValidator,
        IExcelExportService excelExportService)
    {
        _userQueryService = userQueryService;
        _userCommandService = userCommandService;
        _tenantProvider = tenantProvider;
        _idGeneratorAccessor = idGeneratorAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _excelExportService = excelExportService;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.UsersView)]
    public async Task<ActionResult<ApiResponse<PagedResult<UserListItem>>>> Get(
        [FromQuery] UserQueryRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _userQueryService.QueryUsersAsync(request, tenantId, cancellationToken);
        var payload = ApiResponse<PagedResult<UserListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.UsersView)]
    public async Task<ActionResult<ApiResponse<UserDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _userQueryService.GetDetailAsync(id, tenantId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<UserDetail>.Fail(ErrorCodes.NotFound, "User not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<UserDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.UsersCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] UserCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = _idGeneratorAccessor.NextId();
        var createdId = await _userCommandService.CreateAsync(tenantId, request, id, cancellationToken);
        var payload = ApiResponse<object>.Ok(new { Id = createdId.ToString() }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.UsersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] UserUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _userCommandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/roles")]
    [Authorize(Policy = PermissionPolicies.UsersAssignRoles)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRoles(
        long id,
        [FromBody] UserAssignRolesRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _userCommandService.UpdateRolesAsync(
            tenantId,
            id,
            request.RoleIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/departments")]
    [Authorize(Policy = PermissionPolicies.UsersAssignDepartments)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDepartments(
        long id,
        [FromBody] UserAssignDepartmentsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _userCommandService.UpdateDepartmentsAsync(
            tenantId,
            id,
            request.DepartmentIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/positions")]
    [Authorize(Policy = PermissionPolicies.UsersAssignPositions)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePositions(
        long id,
        [FromBody] UserAssignPositionsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _userCommandService.UpdatePositionsAsync(
            tenantId,
            id,
            request.PositionIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.UsersDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _userCommandService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("export")]
    [Authorize(Policy = PermissionPolicies.UsersView)]
    public async Task<IActionResult> Export(
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var bytes = await _excelExportService.ExportUsersAsync(tenantId, keyword, cancellationToken);
        var fileName = $"users_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet("import-template")]
    [Authorize(Policy = PermissionPolicies.UsersCreate)]
    public IActionResult GetImportTemplate()
    {
        var bytes = _excelExportService.GenerateUserImportTemplate();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "user_import_template.xlsx");
    }

    [HttpPost("import")]
    [Authorize(Policy = PermissionPolicies.UsersCreate)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<object>>> Import(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError, "请上传文件", HttpContext.TraceIdentifier));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx")
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError, "仅支持 .xlsx 格式", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var errors = new List<object>();
        int successCount = 0, failureCount = 0, totalRows = 0;

        await using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.First();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        totalRows = lastRow - 1;

        for (var rowNum = 2; rowNum <= lastRow; rowNum++)
        {
            var row = ws.Row(rowNum);
            var username = row.Cell(1).GetValue<string>().Trim();
            var displayName = row.Cell(2).GetValue<string>().Trim();
            var email = row.Cell(3).GetValue<string>().Trim();
            var phone = row.Cell(4).GetValue<string>().Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                errors.Add(new { row = rowNum, field = "用户名", message = "用户名不能为空" });
                failureCount++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                errors.Add(new { row = rowNum, field = "显示名称", message = "显示名称不能为空" });
                failureCount++;
                continue;
            }

            try
            {
                var createRequest = new UserCreateRequest(
                    username,
                    $"Import@{Guid.NewGuid():N8}!",
                    displayName,
                    string.IsNullOrWhiteSpace(email) ? null : email,
                    string.IsNullOrWhiteSpace(phone) ? null : phone,
                    true,
                    [],
                    [],
                    []);

                var newId = _idGeneratorAccessor.NextId();
                await _userCommandService.CreateAsync(tenantId, createRequest, newId, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new { row = rowNum, field = "用户名", message = ex.Message });
                failureCount++;
            }
        }

        var result = new
        {
            totalRows,
            successCount,
            failureCount,
            errors
        };

        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }
}

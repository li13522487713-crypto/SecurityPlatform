using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Controllers.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Authorization;
using ClosedXML.Excel;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController : UsersControllerBase
{
    private readonly IExcelExportService _excelExportService;

    public UsersController(
        IUserQueryService userQueryService,
        IUserCommandService userCommandService,
        ITenantProvider tenantProvider,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor,
        IValidator<UserCreateRequest> createValidator,
        IValidator<UserUpdateRequest> updateValidator,
        IExcelExportService excelExportService)
        : base(
            userQueryService,
            userCommandService,
            tenantProvider,
            idGeneratorAccessor,
            createValidator,
            updateValidator)
    {
        _excelExportService = excelExportService;
    }

    /// <summary>导出用户列表为 Excel</summary>
    [HttpGet("export")]
    [Authorize(Policy = PermissionPolicies.UsersView)]
    public async Task<IActionResult> Export(
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantProvider.GetTenantId();
        var bytes = await _excelExportService.ExportUsersAsync(tenantId, keyword, cancellationToken);
        var fileName = $"users_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    /// <summary>下载用户导入模板</summary>
    [HttpGet("import-template")]
    [Authorize(Policy = PermissionPolicies.UsersCreate)]
    public IActionResult GetImportTemplate()
    {
        var bytes = _excelExportService.GenerateUserImportTemplate();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "user_import_template.xlsx");
    }

    /// <summary>批量导入用户（解析 Excel）</summary>
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

        var tenantId = TenantProvider.GetTenantId();
        var errors = new List<object>();
        int successCount = 0, failureCount = 0, totalRows = 0;

        await using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.First();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        totalRows = lastRow - 1; // 减去表头行

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
                    $"Import@{Guid.NewGuid():N8}!", // 临时随机密码，用户需重置
                    displayName,
                    string.IsNullOrWhiteSpace(email) ? null : email,
                    string.IsNullOrWhiteSpace(phone) ? null : phone,
                    true,
                    [],
                    [],
                    []);

                var newId = IdGeneratorAccessor.NextId();
                await UserCommandService.CreateAsync(tenantId, createRequest, newId, cancellationToken);
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





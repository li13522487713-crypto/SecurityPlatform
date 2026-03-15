using Atlas.Application.Identity.Abstractions;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using ClosedXML.Excel;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 基于 ClosedXML 的 Excel 导出实现
/// </summary>
public sealed class ClosedXmlExcelExportService : IExcelExportService
{
    private readonly IUserQueryService _userQueryService;
    private readonly IDictQueryService _dictQueryService;

    public ClosedXmlExcelExportService(
        IUserQueryService userQueryService,
        IDictQueryService dictQueryService)
    {
        _userQueryService = userQueryService;
        _dictQueryService = dictQueryService;
    }

    public async Task<byte[]> ExportUsersAsync(
        TenantId tenantId, string? keyword = null, CancellationToken ct = default)
    {
        // 最多导出 5000 条
        var result = await _userQueryService.QueryUsersAsync(
            new UserQueryRequest(1, 5000, keyword, null, false, null), tenantId, ct);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("用户列表");

        // 表头
        var headers = new[] { "用户名", "显示名称", "邮箱", "手机号", "状态", "最后登录时间" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // 数据行
        var row = 2;
        foreach (var user in result.Items)
        {
            ws.Cell(row, 1).Value = user.Username;
            ws.Cell(row, 2).Value = user.DisplayName;
            ws.Cell(row, 3).Value = user.Email ?? string.Empty;
            ws.Cell(row, 4).Value = user.PhoneNumber ?? string.Empty;
            ws.Cell(row, 5).Value = user.IsActive ? "启用" : "禁用";
            ws.Cell(row, 6).Value = user.LastLoginAt.HasValue
                ? user.LastLoginAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : string.Empty;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateUserImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("用户导入模板");

        var headers = new[] { "用户名*", "显示名称*", "邮箱", "手机号" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        // 示例行
        ws.Cell(2, 1).Value = "zhangsan";
        ws.Cell(2, 2).Value = "张三";
        ws.Cell(2, 3).Value = "zhangsan@example.com";
        ws.Cell(2, 4).Value = "13800138000";

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> ExportDictDataAsync(
        TenantId tenantId,
        string typeCode,
        string? keyword = null,
        CancellationToken ct = default)
    {
        var result = await _dictQueryService.GetDictDataPagedAsync(
            tenantId,
            typeCode,
            keyword,
            pageIndex: 1,
            pageSize: 5000,
            ct);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add($"字典数据_{typeCode}");

        var headers = new[] { "标签", "值", "排序", "状态", "样式类", "列表样式" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = 2;
        foreach (var item in result.Items)
        {
            ws.Cell(row, 1).Value = item.Label;
            ws.Cell(row, 2).Value = item.Value;
            ws.Cell(row, 3).Value = item.SortOrder;
            ws.Cell(row, 4).Value = item.Status ? "启用" : "禁用";
            ws.Cell(row, 5).Value = item.CssClass ?? string.Empty;
            ws.Cell(row, 6).Value = item.ListClass ?? string.Empty;
            row++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}

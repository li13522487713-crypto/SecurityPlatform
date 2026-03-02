using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IExcelExportService
{
    /// <summary>
    /// 导出用户列表为 Excel 字节流。
    /// </summary>
    Task<byte[]> ExportUsersAsync(TenantId tenantId, string? keyword = null, CancellationToken ct = default);

    /// <summary>
    /// 生成用户导入模板字节流。
    /// </summary>
    byte[] GenerateUserImportTemplate();

    /// <summary>
    /// 导出字典数据为 Excel 字节流。
    /// </summary>
    Task<byte[]> ExportDictDataAsync(
        TenantId tenantId,
        string typeCode,
        string? keyword = null,
        CancellationToken ct = default);
}

using Atlas.Application.BatchProcess.Models;

namespace Atlas.Application.BatchProcess.Abstractions;

/// <summary>
/// Keyset 分页扫描器：基于主键游标实现高效顺序扫描，避免 OFFSET 性能退化。
/// </summary>
public interface IKeysetScanner
{
    Task<KeysetScanResult> ScanAsync(KeysetScanRequest request, CancellationToken cancellationToken);
}

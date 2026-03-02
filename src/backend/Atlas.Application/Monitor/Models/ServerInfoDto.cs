namespace Atlas.Application.Monitor.Models;

public sealed record ServerInfoDto(
    CpuInfoDto Cpu,
    MemoryInfoDto Memory,
    IReadOnlyList<DiskInfoDto> Disks,
    RuntimeInfoDto Runtime);

public sealed record CpuInfoDto(
    int LogicalCores,
    double ProcessCpuUsagePercent);

public sealed record MemoryInfoDto(
    long TotalBytes,
    long UsedBytes,
    long AvailableBytes,
    double UsagePercent);

public sealed record DiskInfoDto(
    string Name,
    long TotalBytes,
    long UsedBytes,
    long AvailableBytes,
    double UsagePercent);

public sealed record RuntimeInfoDto(
    string DotNetVersion,
    string OsDescription,
    string MachineName,
    int ProcessId,
    int ThreadCount,
    long GcMemoryBytes,
    DateTimeOffset StartedAt,
    string Uptime);

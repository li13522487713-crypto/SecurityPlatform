using Atlas.Application.Monitor.Abstractions;
using Atlas.Application.Monitor.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 服务器信息查询服务（基于 System.Diagnostics 和 Environment）
/// </summary>
public sealed class ServerInfoQueryService : IServerInfoQueryService
{
    private static readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private static Process? _currentProcess;

    public Task<ServerInfoDto> GetServerInfoAsync(CancellationToken ct = default)
    {
        _currentProcess ??= Process.GetCurrentProcess();
        _currentProcess.Refresh();

        var cpu = GetCpuInfo();
        var memory = GetMemoryInfo();
        var disks = GetDiskInfo();
        var runtime = GetRuntimeInfo(_currentProcess);

        return Task.FromResult(new ServerInfoDto(cpu, memory, disks, runtime));
    }

    private static CpuInfoDto GetCpuInfo()
    {
        var logicalCores = Environment.ProcessorCount;

        // 简单估算：通过 Process.TotalProcessorTime 相对于实际时间的比例
        // 注意：这是进程级别的 CPU 使用率，非系统级
        double cpuUsage = 0;
        try
        {
            var proc = Process.GetCurrentProcess();
            var startCpuTime = proc.TotalProcessorTime;
            var startTime = DateTime.UtcNow;

            Thread.Sleep(100);

            proc.Refresh();
            var endCpuTime = proc.TotalProcessorTime;
            var endTime = DateTime.UtcNow;

            var cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
            var totalMs = (endTime - startTime).TotalMilliseconds * Environment.ProcessorCount;
            cpuUsage = totalMs > 0 ? Math.Round(cpuUsedMs / totalMs * 100, 1) : 0;
        }
        catch
        {
            cpuUsage = 0;
        }

        return new CpuInfoDto(logicalCores, cpuUsage);
    }

    private static MemoryInfoDto GetMemoryInfo()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        var totalBytes = gcInfo.TotalAvailableMemoryBytes;

        // 使用 GC 分配内存作为已用内存估算
        var usedBytes = GC.GetTotalMemory(false);
        var availableBytes = totalBytes - usedBytes;
        var usagePercent = totalBytes > 0
            ? Math.Round((double)usedBytes / totalBytes * 100, 1)
            : 0;

        return new MemoryInfoDto(totalBytes, usedBytes, availableBytes, usagePercent);
    }

    private static IReadOnlyList<DiskInfoDto> GetDiskInfo()
    {
        var drives = new List<DiskInfoDto>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var total = drive.TotalSize;
                var available = drive.AvailableFreeSpace;
                var used = total - available;
                var usagePercent = total > 0 ? Math.Round((double)used / total * 100, 1) : 0;
                drives.Add(new DiskInfoDto(drive.Name, total, used, available, usagePercent));
            }
        }
        catch
        {
            // ignore drive enumeration errors
        }
        return drives;
    }

    private static RuntimeInfoDto GetRuntimeInfo(Process proc)
    {
        var uptime = DateTimeOffset.UtcNow - _startedAt;
        var uptimeStr = uptime.Days > 0
            ? $"{uptime.Days}天 {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}"
            : $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

        return new RuntimeInfoDto(
            DotNetVersion: RuntimeInformation.FrameworkDescription,
            OsDescription: RuntimeInformation.OSDescription,
            MachineName: Environment.MachineName,
            ProcessId: proc.Id,
            ThreadCount: proc.Threads.Count,
            GcMemoryBytes: GC.GetTotalMemory(false),
            StartedAt: _startedAt,
            Uptime: uptimeStr);
    }
}

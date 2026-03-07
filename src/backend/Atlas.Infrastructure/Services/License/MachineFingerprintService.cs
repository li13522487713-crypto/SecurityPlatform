using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Atlas.Application.License.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Atlas.Infrastructure.Services.License;

/// <summary>
/// 机器指纹服务：采集多维硬件特征并生成稳定哈希值。
/// 支持主指纹 + 次指纹容错，允许 1 个次指纹项目变化（如换网卡）仍然匹配。
/// </summary>
public sealed class MachineFingerprintService : IMachineFingerprintService
{
    private readonly ILogger<MachineFingerprintService> _logger;
    private volatile string? _cachedFingerprint;

    public MachineFingerprintService(ILogger<MachineFingerprintService> logger)
    {
        _logger = logger;
    }

    public string GetCurrentFingerprint()
    {
        if (_cachedFingerprint is not null)
            return _cachedFingerprint;

        var components = CollectFingerprintComponents();
        // 每个组件单独哈希后用 ':' 连接，保留各分量以支持模糊匹配
        var partHashes = components
            .Select(c => string.IsNullOrWhiteSpace(c) ? string.Empty : ComputeSha256(c))
            .ToArray();
        _cachedFingerprint = string.Join(":", partHashes);
        return _cachedFingerprint;
    }

    public bool Matches(string? storedFingerprint)
    {
        if (string.IsNullOrWhiteSpace(storedFingerprint))
        {
            // 证书不绑定机器，任意机器均可使用
            return true;
        }

        var current = GetCurrentFingerprint();

        // 精确匹配
        if (string.Equals(current, storedFingerprint, StringComparison.OrdinalIgnoreCase))
            return true;

        // 容错：逐项比对，允许 1 项次指纹不匹配
        return FuzzyMatch(storedFingerprint, current);
    }

    private bool FuzzyMatch(string storedFingerprint, string currentFingerprint)
    {
        try
        {
            var storedParts = storedFingerprint.Split(':');
            var currentParts = currentFingerprint.Split(':');

            // 旧格式（不含 ':'）无法逐项比对，只能精确匹配，此处已确认不匹配
            if (storedParts.Length < 2 || currentParts.Length < 2)
                return false;

            // 主指纹（索引 0：MachineGuid / machine-id）必须完全一致
            if (!string.Equals(storedParts[0], currentParts[0], StringComparison.OrdinalIgnoreCase))
                return false;

            // 次指纹（MachineName、MAC 地址等）允许 1 项不同。
            // 逐项比对到最长长度，任一侧缺失分量也按不匹配计数，避免长度差异绕过校验。
            var mismatches = 0;
            var maxSecondaryLength = Math.Max(storedParts.Length, currentParts.Length);
            for (var i = 1; i < maxSecondaryLength; i++)
            {
                var storedPart = i < storedParts.Length ? storedParts[i] : null;
                var currentPart = i < currentParts.Length ? currentParts[i] : null;
                if (!string.Equals(storedPart, currentPart, StringComparison.OrdinalIgnoreCase))
                    mismatches++;
            }

            return mismatches <= 1;
        }
        catch
        {
            return false;
        }
    }

    private List<string> CollectFingerprintComponents()
    {
        var components = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            components.Add(GetWindowsMachineGuid());
            components.Add(GetMachineName());
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            components.Add(GetLinuxMachineId());
            components.Add(GetMachineName());
        }
        else
        {
            components.Add(GetMachineName());
        }

        // MAC 地址作为次指纹（容易变化，仅作辅助）
        components.Add(GetPrimaryMacAddress());

        return components;
    }

    [SupportedOSPlatform("windows")]
    private static string GetWindowsMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography", writable: false);
            return key?.GetValue("MachineGuid")?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetLinuxMachineId()
    {
        try
        {
            var path = "/etc/machine-id";
            if (File.Exists(path))
                return File.ReadAllText(path).Trim();
        }
        catch { }
        return string.Empty;
    }

    private static string GetMachineName()
    {
        try { return Environment.MachineName; }
        catch { return string.Empty; }
    }

    private static string GetPrimaryMacAddress()
    {
        try
        {
            var mac = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic =>
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    nic.OperationalStatus == OperationalStatus.Up &&
                    !nic.Description.Contains("virtual", StringComparison.OrdinalIgnoreCase) &&
                    !nic.Description.Contains("hyper-v", StringComparison.OrdinalIgnoreCase))
                .OrderBy(nic => nic.Description)
                .FirstOrDefault()
                ?.GetPhysicalAddress()
                ?.ToString();
            return mac ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

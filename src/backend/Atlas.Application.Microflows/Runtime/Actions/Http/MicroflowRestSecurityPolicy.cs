using System.Net;
using System.Net.Sockets;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Actions.Http;

public sealed record MicroflowRestSecurityDecision
{
    public bool Allowed { get; init; }
    public string ReasonCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = "error";
    public string? NormalizedUrl { get; init; }
}

public sealed class MicroflowRestSecurityPolicy
{
    private static readonly HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "host",
        "content-length",
        "transfer-encoding",
        "connection"
    };

    public async Task<MicroflowRestSecurityDecision> EvaluateAsync(
        MicroflowRuntimeHttpRequest request,
        MicroflowRuntimeHttpOptions options,
        bool resolveHostAddresses,
        CancellationToken ct)
    {
        var urlDecision = await EvaluateUrlAsync(request.Url, options, resolveHostAddresses, ct);
        if (!urlDecision.Allowed)
        {
            return urlDecision;
        }

        foreach (var header in request.Headers)
        {
            var headerDecision = EvaluateHeader(header.Key, header.Value, options);
            if (!headerDecision.Allowed)
            {
                return headerDecision;
            }
        }

        return urlDecision;
    }

    public async Task<MicroflowRestSecurityDecision> EvaluateUrlAsync(
        string url,
        MicroflowRuntimeHttpOptions options,
        bool resolveHostAddresses,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Block(RuntimeErrorCode.RuntimeRestInvalidUrl, "REST URL 不能为空。");
        }

        if (url.Length > Math.Max(1, options.MaxUrlLength))
        {
            return Block(RuntimeErrorCode.RuntimeRestInvalidUrl, $"REST URL 长度超过限制 {options.MaxUrlLength}。");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Block(RuntimeErrorCode.RuntimeRestInvalidUrl, "REST URL 不是合法绝对 URL。");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Block(RuntimeErrorCode.RuntimeRestUnsupportedScheme, $"REST URL scheme '{uri.Scheme}' 不被允许。");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return Block(RuntimeErrorCode.RuntimeRestInvalidUrl, "REST URL host 不能为空。");
        }

        var host = uri.Host.TrimEnd('.').ToLowerInvariant();
        if (HostMatches(host, options.DeniedHosts))
        {
            return Block(RuntimeErrorCode.RuntimeRestDeniedHost, $"REST host '{host}' 命中 denylist。", uri);
        }

        if (options.AllowedHosts.Count > 0 && !HostMatches(host, options.AllowedHosts))
        {
            return Block(RuntimeErrorCode.RuntimeRestDeniedHost, $"REST host '{host}' 不在 allowlist。", uri);
        }

        if (!options.AllowPrivateNetwork && IsLocalHostName(host))
        {
            return Block(RuntimeErrorCode.RuntimeRestPrivateNetworkBlocked, $"REST host '{host}' 默认被 SSRF 策略拒绝。", uri);
        }

        if (!options.AllowPrivateNetwork && IPAddress.TryParse(host, out var parsedAddress) && IsBlockedAddress(parsedAddress))
        {
            return Block(RuntimeErrorCode.RuntimeRestPrivateNetworkBlocked, $"REST host '{host}' 指向私有或本地地址。", uri);
        }

        if (!options.AllowPrivateNetwork && resolveHostAddresses)
        {
            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host, ct);
            }
            catch (SocketException ex)
            {
                return Block(RuntimeErrorCode.RuntimeRestUrlBlocked, $"REST host '{host}' DNS 解析失败：{ex.Message}", uri);
            }

            if (addresses.Any(IsBlockedAddress))
            {
                return Block(RuntimeErrorCode.RuntimeRestPrivateNetworkBlocked, $"REST host '{host}' 解析到私有或本地地址。", uri);
            }
        }

        return new MicroflowRestSecurityDecision
        {
            Allowed = true,
            ReasonCode = "allowed",
            Message = "REST URL passed runtime security policy.",
            Severity = "info",
            NormalizedUrl = uri.AbsoluteUri
        };
    }

    private static MicroflowRestSecurityDecision EvaluateHeader(
        string name,
        string value,
        MicroflowRuntimeHttpOptions options)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Block(RuntimeErrorCode.RuntimeRestUrlBlocked, "REST header key 不能为空。");
        }

        if (ForbiddenHeaders.Contains(name))
        {
            return Block(RuntimeErrorCode.RuntimeRestUrlBlocked, $"REST header '{name}' 不允许由微流设置。");
        }

        if (name.Any(ch => char.IsControl(ch) || ch is ':' or '\r' or '\n'))
        {
            return Block(RuntimeErrorCode.RuntimeRestUrlBlocked, $"REST header '{name}' 含非法字符。");
        }

        if (value.Length > Math.Max(1, options.MaxHeaderValueLength))
        {
            return Block(RuntimeErrorCode.RuntimeRestUrlBlocked, $"REST header '{name}' value 超过长度限制。");
        }

        return new MicroflowRestSecurityDecision { Allowed = true, ReasonCode = "allowed", Message = "Header allowed.", Severity = "info" };
    }

    private static bool HostMatches(string host, IReadOnlyList<string> patterns)
    {
        foreach (var rawPattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(rawPattern))
            {
                continue;
            }

            var pattern = rawPattern.Trim().TrimEnd('.').ToLowerInvariant();
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = pattern[1..];
                if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                continue;
            }

            if (string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLocalHostName(string host)
        => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "localhost.localdomain", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address))
        {
            return true;
        }

        if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal)
        {
            return true;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || bytes[0] == 127
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254)
            || bytes[0] == 0
            || bytes[0] >= 224;
    }

    private static MicroflowRestSecurityDecision Block(string code, string message, Uri? uri = null)
        => new()
        {
            Allowed = false,
            ReasonCode = code,
            Message = message,
            Severity = "error",
            NormalizedUrl = uri?.AbsoluteUri
        };
}

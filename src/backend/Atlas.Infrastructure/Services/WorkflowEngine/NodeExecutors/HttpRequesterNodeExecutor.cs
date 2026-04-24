using System.Text;
using System.Net;
using System.Net.Sockets;
using Atlas.Domain.AiPlatform.Enums;
using System.IO;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// HTTP 请求节点：发起 HTTP 调用。
/// Config 参数：url、method（GET/POST/PUT/DELETE）、headers（JSON 字符串）、body
/// 输出变量：http_status_code、http_response_body
/// </summary>
public sealed class HttpRequesterNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.HttpRequester;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        var urlTemplate = context.GetConfigString("url");
        var method = context.GetConfigString("method", "GET");
        var bodyTemplate = context.GetConfigString("body");
        var headers = ResolveHeaders(context);

        var url = context.ReplaceVariables(urlTemplate);
        var body = context.ReplaceVariables(bodyTemplate);

        if (string.IsNullOrWhiteSpace(url))
        {
            return new NodeExecutionResult(false, outputs, "HTTP 请求 URL 为空");
        }

        var allowLoopback = context.GetConfigBoolean("allowLoopback", false);
        var (isAllowed, targetUri, allowedAddresses, validationError) = await ValidateOutboundUrlAsync(url, allowLoopback, cancellationToken);
        if (!isAllowed || targetUri is null || allowedAddresses.Count == 0)
        {
            return new NodeExecutionResult(false, outputs, validationError ?? "HTTP 请求 URL 非法或不被允许");
        }

        try
        {
            using var client = CreatePinnedHttpClient(allowedAddresses);
            client.Timeout = TimeSpan.FromSeconds(30);

            var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), targetUri);
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            if (!string.IsNullOrWhiteSpace(body) && method.ToUpperInvariant() is "POST" or "PUT" or "PATCH")
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            outputs["http_status_code"] = JsonSerializer.SerializeToElement((int)response.StatusCode);
            outputs["http_response_body"] = VariableResolver.CreateStringElement(responseBody);

            return new NodeExecutionResult(true, outputs);
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult(false, outputs, $"HTTP 请求失败: {ex.Message}");
        }
    }

    private static Dictionary<string, string> ResolveHeaders(NodeExecutionContext context)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!VariableResolver.TryGetConfigValue(context.Node.Config, "headers", out var raw))
        {
            return headers;
        }

        if (raw.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in raw.EnumerateObject())
            {
                var value = VariableResolver.ToDisplayText(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    headers[property.Name] = context.ReplaceVariables(value);
                }
            }

            return headers;
        }

        var text = VariableResolver.ToDisplayText(raw).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return headers;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return headers;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var value = VariableResolver.ToDisplayText(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    headers[property.Name] = context.ReplaceVariables(value);
                }
            }
        }
        catch
        {
            // headers 非 JSON 时忽略，保持向后兼容
        }

        return headers;
    }

    private static async Task<(bool IsAllowed, Uri? Uri, IReadOnlyList<IPAddress> AllowedAddresses, string? Error)> ValidateOutboundUrlAsync(
        string url,
        bool allowLoopback,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUri))
        {
            return (false, null, Array.Empty<IPAddress>(), "HTTP 请求 URL 格式非法，必须是绝对地址");
        }

        if (!string.Equals(parsedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, Array.Empty<IPAddress>(), "仅允许发起 HTTP/HTTPS 请求");
        }

        if (!allowLoopback && (parsedUri.IsLoopback || string.Equals(parsedUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)))
        {
            return (false, null, Array.Empty<IPAddress>(), "不允许访问本机回环地址");
        }

        if (!string.IsNullOrWhiteSpace(parsedUri.UserInfo))
        {
            return (false, null, Array.Empty<IPAddress>(), "URL 不允许包含用户名或密码信息");
        }

        if (IPAddress.TryParse(parsedUri.Host, out var literalIp))
        {
            if (IsBlockedAddress(literalIp, allowLoopback))
            {
                return (false, null, Array.Empty<IPAddress>(), "目标地址属于内网/链路本地/保留网段，已被安全策略拒绝");
            }

            return (true, parsedUri, new[] { literalIp }, null);
        }

        IPAddress[] resolvedAddresses;
        try
        {
            resolvedAddresses = await Dns.GetHostAddressesAsync(parsedUri.DnsSafeHost, cancellationToken);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return (false, null, Array.Empty<IPAddress>(), "目标主机解析失败，无法发起请求");
        }

        if (resolvedAddresses.Length == 0)
        {
            return (false, null, Array.Empty<IPAddress>(), "目标主机未解析到可用地址");
        }

        foreach (var ip in resolvedAddresses)
        {
            if (IsBlockedAddress(ip, allowLoopback))
            {
                return (false, null, Array.Empty<IPAddress>(), "目标主机解析到受限地址（内网/回环/链路本地/保留网段），请求被拒绝");
            }
        }

        var ordered = resolvedAddresses
            .OrderBy(ip => ip.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
            .ToArray();
        return (true, parsedUri, ordered, null);
    }

    private static HttpClient CreatePinnedHttpClient(IReadOnlyList<IPAddress> allowedAddresses)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectCallback = async (context, cancellationToken) =>
            {
                Exception? lastException = null;

                foreach (var address in allowedAddresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception ex)
                    {
                        socket.Dispose();
                        lastException = ex;
                    }
                }

                throw new HttpRequestException("无法连接到目标主机的已验证地址。", lastException);
            }
        };

        return new HttpClient(handler, disposeHandler: true);
    }

    private static bool IsBlockedAddress(IPAddress ip, bool allowLoopback)
    {
        if (!allowLoopback && IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            var first = bytes[0];
            var second = bytes[1];
            var third = bytes[2];

            // 私有网段、回环、链路本地、共享地址空间、保留地址、多播、基准测试网段等全部拒绝。
            return first == 0 ||
                   first == 10 ||
                   (!allowLoopback && first == 127) ||
                   (first == 100 && second is >= 64 and <= 127) ||
                   (first == 169 && second == 254) ||
                   (first == 172 && second is >= 16 and <= 31) ||
                   (first == 192 && second == 168) ||
                   (first == 192 && second == 0 && third == 0) ||
                   (first == 198 && second is 18 or 19) ||
                   first >= 224;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if ((!allowLoopback && ip.Equals(IPAddress.IPv6Loopback)) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
            {
                return true;
            }

            var bytes = ip.GetAddressBytes();
            // Unique local address: fc00::/7
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return true;
            }

            // Link-local: fe80::/10
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
            {
                return true;
            }
        }

        return false;
    }
}

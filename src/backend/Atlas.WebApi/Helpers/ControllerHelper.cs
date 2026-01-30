using System.Security.Claims;
using Atlas.Core.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Helpers;

/// <summary>
/// 控制器辅助方法
/// </summary>
public static class ControllerHelper
{
    private const string ClientContextItemKey = "ClientContext";
    private const string ClientTypeHeader = "X-Client-Type";
    private const string ClientPlatformHeader = "X-Client-Platform";
    private const string ClientChannelHeader = "X-Client-Channel";
    private const string ClientAgentHeader = "X-Client-Agent";
    /// <summary>
    /// 安全地获取当前用户ID（从JWT Claims中）
    /// </summary>
    /// <param name="user">ClaimsPrincipal</param>
    /// <returns>用户ID，如果无法解析则返回null</returns>
    public static long? GetUserIdSafely(ClaimsPrincipal? user)
    {
        if (user == null) return null;

        var candidates = new[]
        {
            user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value,
            user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier && long.TryParse(c.Value, out _))?.Value,
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            user.FindFirst(ClaimTypes.Name)?.Value
        };

        foreach (var v in candidates)
        {
            if (!string.IsNullOrWhiteSpace(v) && long.TryParse(v, out var id))
                return id;
        }

        return null;
    }

    /// <summary>
    /// 获取当前用户ID（如果无法解析则抛出异常）
    /// </summary>
    /// <param name="user">ClaimsPrincipal</param>
    /// <returns>用户ID</returns>
    /// <exception cref="UnauthorizedAccessException">当无法获取用户ID时抛出</exception>
    public static long GetUserIdOrThrow(ClaimsPrincipal? user)
    {
        var userId = GetUserIdSafely(user);
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("无法获取用户ID，请重新登录");
        }

        return userId.Value;
    }

    /// <summary>
    /// 检查用户是否具有指定角色
    /// </summary>
    public static bool IsInRole(ClaimsPrincipal? user, string role)
    {
        return user?.IsInRole(role) == true;
    }

    /// <summary>
    /// 获取请求的IP地址
    /// </summary>
    public static string? GetIpAddress(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// 获取请求的User-Agent
    /// </summary>
    public static string? GetUserAgent(HttpContext context)
    {
        return context.Request.Headers.UserAgent.ToString();
    }

    public static ClientContext GetClientContext(HttpContext context)
    {
        if (context.Items.TryGetValue(ClientContextItemKey, out var cached)
            && cached is ClientContext cachedContext)
        {
            return cachedContext;
        }

        var headers = context.Request.Headers;
        var userAgent = GetUserAgent(context) ?? string.Empty;

        var clientType = TryParseHeader(headers, ClientTypeHeader, out ClientType type)
            ? type
            : ClientType.WebH5;

        var platform = TryParseHeader(headers, ClientPlatformHeader, out ClientPlatform platformValue)
            ? platformValue
            : DetectPlatform(userAgent);

        var channel = TryParseHeader(headers, ClientChannelHeader, out ClientChannel channelValue)
            ? channelValue
            : ClientChannel.Browser;

        var agent = TryParseHeader(headers, ClientAgentHeader, out ClientAgent agentValue)
            ? agentValue
            : DetectAgent(userAgent);

        var resolved = new ClientContext(clientType, platform, channel, agent);
        context.Items[ClientContextItemKey] = resolved;
        return resolved;
    }

    private static bool TryParseHeader<TEnum>(IHeaderDictionary headers, string headerName, out TEnum value)
        where TEnum : struct
    {
        value = default;
        if (!headers.TryGetValue(headerName, out var rawValue))
        {
            return false;
        }

        var text = rawValue.ToString();
        return Enum.TryParse(text, ignoreCase: true, out value);
    }

    private static ClientPlatform DetectPlatform(string userAgent)
    {
        var ua = userAgent.ToLowerInvariant();
        if (ua.Contains("android"))
        {
            return ClientPlatform.Android;
        }

        if (ua.Contains("iphone") || ua.Contains("ipad") || ua.Contains("ipod"))
        {
            return ClientPlatform.iOS;
        }

        return ClientPlatform.Web;
    }

    private static ClientAgent DetectAgent(string userAgent)
    {
        var ua = userAgent.ToLowerInvariant();
        if (ua.Contains("edg/"))
        {
            return ClientAgent.Edge;
        }

        if (ua.Contains("chrome/") && !ua.Contains("edg/"))
        {
            return ClientAgent.Chrome;
        }

        if (ua.Contains("firefox/"))
        {
            return ClientAgent.Firefox;
        }

        if (ua.Contains("safari/") && !ua.Contains("chrome/"))
        {
            return ClientAgent.Safari;
        }

        return ClientAgent.Other;
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Helpers;

/// <summary>
/// 控制器辅助方法
/// </summary>
public static class ControllerHelper
{
    /// <summary>
    /// 安全地获取当前用户ID（从JWT Claims中）
    /// </summary>
    /// <param name="user">ClaimsPrincipal</param>
    /// <returns>用户ID，如果无法解析则返回null</returns>
    public static long? GetUserIdSafely(ClaimsPrincipal? user)
    {
        if (user == null)
        {
            return null;
        }

        var claimValue = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return null;
        }

        if (long.TryParse(claimValue, out var userId))
        {
            return userId;
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
}

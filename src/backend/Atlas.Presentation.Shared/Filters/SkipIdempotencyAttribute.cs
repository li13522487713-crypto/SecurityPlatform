using Microsoft.AspNetCore.Mvc.Filters;

namespace Atlas.Presentation.Shared.Filters;

/// <summary>
/// 已废止的兼容标记。
/// 运行时幂等过滤器已移除，保留该特性仅用于兼容既有控制器声明。
/// </summary>
public sealed class SkipIdempotencyAttribute : Attribute, IFilterMetadata
{
}

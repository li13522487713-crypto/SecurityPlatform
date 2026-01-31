using Microsoft.AspNetCore.Mvc.Filters;

namespace Atlas.WebApi.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipIdempotencyAttribute : Attribute, IFilterMetadata
{
}

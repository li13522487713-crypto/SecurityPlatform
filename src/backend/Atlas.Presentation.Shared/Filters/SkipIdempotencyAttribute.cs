using Microsoft.AspNetCore.Mvc.Filters;

namespace Atlas.Presentation.Shared.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipIdempotencyAttribute : Attribute, IFilterMetadata
{
}

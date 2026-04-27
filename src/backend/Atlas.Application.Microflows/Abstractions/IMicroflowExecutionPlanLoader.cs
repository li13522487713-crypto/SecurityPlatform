using System.Text.Json;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowExecutionPlanLoader
{
    Task<MicroflowExecutionPlan> LoadCurrentAsync(
        string resourceId,
        MicroflowExecutionPlanLoadOptions options,
        CancellationToken cancellationToken);

    Task<MicroflowExecutionPlan> LoadVersionAsync(
        string resourceId,
        string versionId,
        MicroflowExecutionPlanLoadOptions options,
        CancellationToken cancellationToken);

    Task<MicroflowExecutionPlan> LoadFromSchemaAsync(
        JsonElement schema,
        MicroflowExecutionPlanLoadOptions options,
        CancellationToken cancellationToken);
}

public interface IMicroflowRuntimeDtoBuilder
{
    MicroflowRuntimeDto Build(
        JsonElement schema,
        MicroflowExecutionPlanLoadOptions options);
}

public interface IMicroflowExecutionPlanBuilder
{
    MicroflowExecutionPlan Build(
        MicroflowRuntimeDto runtimeDto,
        MicroflowExecutionPlanLoadOptions options);
}

public interface IMicroflowExecutionPlanValidator
{
    MicroflowExecutionPlanValidationResult Validate(
        MicroflowExecutionPlan plan,
        MicroflowExecutionPlanLoadOptions options);
}

public interface IMicroflowActionSupportMatrix
{
    MicroflowActionSupportDescriptor Resolve(string? actionKind, string? officialType, MicroflowExecutionPlanLoadOptions options);
}

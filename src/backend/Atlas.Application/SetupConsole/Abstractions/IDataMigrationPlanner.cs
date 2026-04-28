using Atlas.Application.SetupConsole.Models;
using Atlas.Domain.Setup.Entities;

namespace Atlas.Application.SetupConsole.Abstractions;

public interface IDataMigrationPlanner
{
    Task<DataMigrationPlan> PlanAsync(
        DataMigrationJob job,
        ResolvedMigrationConnection source,
        ResolvedMigrationConnection target,
        CancellationToken cancellationToken = default);
}

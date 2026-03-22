using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Application.DynamicTables;

/// <summary>
/// Provides unified validation and expression execution for dynamic form payloads.
/// Currently serves as a mock/interface for Phase 1 P0, reserved for Phase 2 dynamic rules engine execution based on frontend expressions.
/// </summary>
public interface IDynamicFormValidationService
{
    /// <summary>
    /// Validates the dynamic record payload based on dynamically configured form rules and expressions.
    /// </summary>
    Task<bool> ValidateAsync(string tableKey, IDictionary<string, object> payload, CancellationToken cancellationToken = default);
}

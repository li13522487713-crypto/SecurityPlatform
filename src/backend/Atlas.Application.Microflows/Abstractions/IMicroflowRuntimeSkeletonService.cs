using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowRuntimeSkeletonService
{
    Task TestRunAsync(string id, TestRunMicroflowRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowRunSessionDto> GetRunAsync(string runId, CancellationToken cancellationToken);

    Task<MicroflowRunTraceResponseDto> GetTraceAsync(string runId, CancellationToken cancellationToken);

    Task<CancelMicroflowRunResponseDto> CancelAsync(string runId, CancellationToken cancellationToken);
}

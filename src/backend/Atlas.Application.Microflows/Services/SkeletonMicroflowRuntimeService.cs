using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Services;

public sealed class SkeletonMicroflowRuntimeService : IMicroflowRuntimeSkeletonService
{
    public Task TestRunAsync(string id, TestRunMicroflowRequestDto request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new MicroflowApiException(
            MicroflowApiErrorCode.MicroflowServiceUnavailable,
            "微流试运行服务尚未启用，将在后续 Runtime 轮次实现。",
            StatusCodes.Status503ServiceUnavailable,
            retryable: false);
    }

    public Task<MicroflowRunSessionDto> GetRunAsync(string runId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new MicroflowApiException(
            MicroflowApiErrorCode.MicroflowNotFound,
            "微流运行会话不存在。",
            StatusCodes.Status404NotFound);
    }

    public Task<MicroflowRunTraceResponseDto> GetTraceAsync(string runId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new MicroflowApiException(
            MicroflowApiErrorCode.MicroflowNotFound,
            "微流运行 Trace 不存在。",
            StatusCodes.Status404NotFound);
    }

    public Task<CancelMicroflowRunResponseDto> CancelAsync(string runId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new MicroflowApiException(
            MicroflowApiErrorCode.MicroflowServiceUnavailable,
            "微流运行取消服务尚未启用，将在后续 Runtime 轮次实现。",
            StatusCodes.Status503ServiceUnavailable);
    }
}

internal static class StatusCodes
{
    public const int Status404NotFound = 404;
    public const int Status503ServiceUnavailable = 503;
}

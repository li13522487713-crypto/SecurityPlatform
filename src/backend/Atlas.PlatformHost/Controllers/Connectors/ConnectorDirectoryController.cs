using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Connectors.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Connectors;

[ApiController]
[Authorize]
[Route("api/v1/connectors/providers/{providerId:long}/directory")]
public sealed class ConnectorDirectoryController : ControllerBase
{
    private readonly IExternalDirectorySyncService _syncService;

    public ConnectorDirectoryController(IExternalDirectorySyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpPost("sync/full")]
    public async Task<ActionResult<ExternalDirectorySyncJobResponse>> RunFullSyncAsync(long providerId, CancellationToken cancellationToken)
        => Ok(await _syncService.RunFullSyncAsync(providerId, "manual", cancellationToken).ConfigureAwait(false));

    [HttpPost("sync/incremental")]
    public async Task<ActionResult<ExternalDirectorySyncJobResponse>> ApplyIncrementalAsync(
        long providerId,
        [FromBody] ExternalDirectoryEvent evt,
        CancellationToken cancellationToken)
        => Ok(await _syncService.ApplyIncrementalEventAsync(providerId, evt, "manual", cancellationToken).ConfigureAwait(false));

    [HttpGet("sync/jobs")]
    public async Task<ActionResult<IReadOnlyList<ExternalDirectorySyncJobResponse>>> ListRecentAsync(long providerId, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 200) take = 20;
        return Ok(await _syncService.ListRecentAsync(providerId, take, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("sync/jobs/{jobId:long}")]
    public async Task<ActionResult<ExternalDirectorySyncJobResponse>> GetJobAsync(long providerId, long jobId, CancellationToken cancellationToken)
    {
        var job = await _syncService.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        return job is null || job.ProviderId != providerId ? NotFound() : Ok(job);
    }

    [HttpGet("sync/jobs/{jobId:long}/diffs")]
    public async Task<ActionResult<object>> ListDiffsAsync(long providerId, long jobId, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;
        var skip = (pageIndex - 1) * pageSize;
        var items = await _syncService.ListJobDiffsAsync(jobId, skip, pageSize, cancellationToken).ConfigureAwait(false);
        var total = await _syncService.CountJobDiffsAsync(jobId, cancellationToken).ConfigureAwait(false);
        return Ok(new { items, total, pageIndex, pageSize });
    }
}

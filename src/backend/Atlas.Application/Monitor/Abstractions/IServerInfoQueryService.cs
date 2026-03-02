using Atlas.Application.Monitor.Models;

namespace Atlas.Application.Monitor.Abstractions;

public interface IServerInfoQueryService
{
    Task<ServerInfoDto> GetServerInfoAsync(CancellationToken ct = default);
}

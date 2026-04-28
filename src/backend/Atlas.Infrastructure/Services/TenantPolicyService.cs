using System.Text.Json;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

public sealed class TenantPolicyService : ITenantPolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TenantNetworkPolicyRepository _networkRepository;
    private readonly TenantDataResidencyPolicyRepository _residencyRepository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public TenantPolicyService(
        TenantNetworkPolicyRepository networkRepository,
        TenantDataResidencyPolicyRepository residencyRepository,
        IIdGeneratorAccessor idGenerator)
    {
        _networkRepository = networkRepository;
        _residencyRepository = residencyRepository;
        _idGenerator = idGenerator;
    }

    public async Task<TenantNetworkPolicyDto?> GetNetworkAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _networkRepository.FindByTenantAsync(tenantId, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<TenantNetworkPolicyDto> UpsertNetworkAsync(TenantId tenantId, long updatedBy, TenantNetworkPolicyUpdateRequest request, CancellationToken cancellationToken)
    {
        var allowJson = JsonSerializer.Serialize(request.Allowlist ?? Array.Empty<string>(), JsonOptions);
        var denyJson = JsonSerializer.Serialize(request.Denylist ?? Array.Empty<string>(), JsonOptions);
        var existing = await _networkRepository.FindByTenantAsync(tenantId, cancellationToken);
        if (existing is null)
        {
            var entity = new TenantNetworkPolicy(tenantId, request.Mode, allowJson, denyJson, updatedBy, _idGenerator.NextId());
            await _networkRepository.AddAsync(entity, cancellationToken);
            return ToDto(entity);
        }
        existing.Update(request.Mode, allowJson, denyJson, updatedBy);
        await _networkRepository.UpdateAsync(existing, cancellationToken);
        return ToDto(existing);
    }

    public async Task<TenantDataResidencyPolicyDto?> GetResidencyAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _residencyRepository.FindByTenantAsync(tenantId, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<TenantDataResidencyPolicyDto> UpsertResidencyAsync(TenantId tenantId, long updatedBy, TenantDataResidencyPolicyUpdateRequest request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.AllowedRegions, JsonOptions);
        var existing = await _residencyRepository.FindByTenantAsync(tenantId, cancellationToken);
        if (existing is null)
        {
            var entity = new TenantDataResidencyPolicy(tenantId, json, request.Notes, updatedBy, _idGenerator.NextId());
            await _residencyRepository.AddAsync(entity, cancellationToken);
            return ToDto(entity);
        }
        existing.Update(json, request.Notes, updatedBy);
        await _residencyRepository.UpdateAsync(existing, cancellationToken);
        return ToDto(existing);
    }

    internal static TenantNetworkPolicyDto ToDto(TenantNetworkPolicy entity)
    {
        return new TenantNetworkPolicyDto(
            Id: entity.Id.ToString(),
            Mode: entity.Mode,
            Allowlist: ParseList(entity.AllowlistJson),
            Denylist: ParseList(entity.DenylistJson),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
    }

    internal static TenantDataResidencyPolicyDto ToDto(TenantDataResidencyPolicy entity)
    {
        return new TenantDataResidencyPolicyDto(
            Id: entity.Id.ToString(),
            AllowedRegions: ParseList(entity.AllowedRegionsJson),
            Notes: string.IsNullOrEmpty(entity.Notes) ? null : entity.Notes,
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
    }

    private static IReadOnlyList<string> ParseList(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}

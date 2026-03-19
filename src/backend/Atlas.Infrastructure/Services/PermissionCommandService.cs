using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class PermissionCommandService : IPermissionCommandService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IPermissionDecisionService _permissionDecisionService;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public PermissionCommandService(
        IPermissionRepository permissionRepository,
        IPermissionDecisionService permissionDecisionService,
        IAuditWriter auditWriter,
        ICurrentUserAccessor currentUserAccessor)
    {
        _permissionRepository = permissionRepository;
        _permissionDecisionService = permissionDecisionService;
        _auditWriter = auditWriter;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        PermissionCreateRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var existing = await _permissionRepository.FindByCodeAsync(tenantId, request.Code, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException("Permission code already exists.", ErrorCodes.ValidationError);
        }

        var permission = new Permission(tenantId, request.Name, request.Code, request.Type, id);
        permission.Update(request.Name, request.Type, request.Description);
        await _permissionRepository.AddAsync(permission, cancellationToken);
        await _permissionDecisionService.InvalidateTenantAsync(tenantId, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            "Identity.Permission.Created",
            $"permissionId={permission.Id};code={request.Code};type={request.Type}",
            cancellationToken);
        return permission.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long permissionId,
        PermissionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var permission = await _permissionRepository.FindByIdAsync(tenantId, permissionId, cancellationToken);
        if (permission is null)
        {
            throw new BusinessException("Permission not found.", ErrorCodes.NotFound);
        }

        permission.Update(request.Name, request.Type, request.Description);
        await _permissionRepository.UpdateAsync(permission, cancellationToken);
        await _permissionDecisionService.InvalidateTenantAsync(tenantId, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            "Identity.Permission.Updated",
            $"permissionId={permissionId};code={permission.Code};type={request.Type}",
            cancellationToken);
    }

    private async Task WriteAuditAsync(
        TenantId tenantId,
        string action,
        string target,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        var actor = currentUser?.Username ?? "system";
        var record = new AuditRecord(
            tenantId,
            actor,
            action,
            "Success",
            target,
            null,
            null);
        await _auditWriter.WriteAsync(record, cancellationToken);
    }
}

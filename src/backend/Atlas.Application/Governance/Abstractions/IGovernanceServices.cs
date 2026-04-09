using Atlas.Application.Governance.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Governance.Abstractions;

public interface IPackageService
{
    Task<PackageOperationResponse> ExportAsync(TenantId tenantId, long userId, PackageExportRequest request, CancellationToken cancellationToken = default);
    Task<PackageOperationResponse> ImportAsync(TenantId tenantId, long userId, PackageImportRequest request, CancellationToken cancellationToken = default);
    Task<PackageOperationResponse> AnalyzeAsync(TenantId tenantId, long userId, PackageAnalyzeRequest request, CancellationToken cancellationToken = default);
}

public interface ILicenseGrantService
{
    Task<string> CreateOfflineRequestAsync(TenantId tenantId, long userId, LicenseOfflineRequest request, CancellationToken cancellationToken = default);
    Task<LicenseValidateResponse> ImportAsync(TenantId tenantId, long userId, LicenseImportRequest request, CancellationToken cancellationToken = default);
    Task<LicenseValidateResponse> ValidateAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}

public interface IToolAuthorizationService
{
    Task<PagedResult<ToolAuthorizationPolicyResponse>> QueryPoliciesAsync(TenantId tenantId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<string> CreatePolicyAsync(TenantId tenantId, long userId, ToolAuthorizationPolicyRequest request, CancellationToken cancellationToken = default);
    Task UpdatePolicyAsync(TenantId tenantId, long userId, long id, ToolAuthorizationPolicyRequest request, CancellationToken cancellationToken = default);
    Task<ToolAuthorizationSimulateResponse> SimulateAsync(TenantId tenantId, ToolAuthorizationSimulateRequest request, CancellationToken cancellationToken = default);
}

public interface IDlpService
{
    Task<IReadOnlyList<DataClassificationResponse>> GetClassificationsAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<string> CreateClassificationAsync(TenantId tenantId, long userId, DataClassificationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SensitiveLabelResponse>> GetLabelsAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<string> CreateLabelAsync(TenantId tenantId, long userId, SensitiveLabelRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DlpPolicyResponse>> GetPoliciesAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<string> CreatePolicyAsync(TenantId tenantId, long userId, DlpPolicyRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboundChannelResponse>> GetOutboundChannelsAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<string> CreateOutboundChannelAsync(TenantId tenantId, long userId, OutboundChannelRequest request, CancellationToken cancellationToken = default);
    Task<object> BindMaskPolicyAsync(TenantId tenantId, long userId, DlpBindingRequest request, CancellationToken cancellationToken = default);
    Task<object> CreateExportJobAsync(TenantId tenantId, long userId, DlpTransferJobRequest request, CancellationToken cancellationToken = default);
    Task<object> CreateDownloadJobAsync(TenantId tenantId, long userId, DlpTransferJobRequest request, CancellationToken cancellationToken = default);
    Task<object> CreateExternalShareApprovalAsync(TenantId tenantId, long userId, ExternalShareApprovalRequest request, CancellationToken cancellationToken = default);
    Task<OutboundCheckResponse> CheckOutboundAsync(TenantId tenantId, long userId, OutboundCheckRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeakageEventResponse>> GetEventsAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvidencePackageResponse>> GetEvidencePackagesAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}

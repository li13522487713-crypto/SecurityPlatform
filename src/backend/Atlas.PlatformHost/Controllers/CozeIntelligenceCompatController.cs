using Atlas.Application.Platform.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Controllers.Ai;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/intelligence_api")]
public sealed class CozeIntelligenceCompatController : CozeIntelligenceCompatControllerBase
{
    public CozeIntelligenceCompatController(
        IWorkspacePortalService workspacePortalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
        : base(workspacePortalService, tenantProvider, currentUserAccessor)
    {
    }
}

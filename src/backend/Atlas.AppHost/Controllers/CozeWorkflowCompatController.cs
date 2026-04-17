using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.Platform.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Controllers.Ai;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/workflow_api")]
public sealed class CozeWorkflowCompatController : CozeWorkflowCompatControllerBase
{
    public CozeWorkflowCompatController(
        IDagWorkflowCommandService commandService,
        IDagWorkflowQueryService queryService,
        IDagWorkflowExecutionService executionService,
        ICanvasValidator canvasValidator,
        IWorkspacePortalService workspacePortalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
        : base(commandService, queryService, executionService, canvasValidator, workspacePortalService, tenantProvider, currentUserAccessor)
    {
    }
}

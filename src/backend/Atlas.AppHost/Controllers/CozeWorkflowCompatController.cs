using Atlas.Application.AiPlatform.Abstractions;
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
        IWorkflowV2CommandService commandService,
        IWorkflowV2QueryService queryService,
        IWorkflowV2ExecutionService executionService,
        ICanvasValidator canvasValidator,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
        : base(commandService, queryService, executionService, canvasValidator, tenantProvider, currentUserAccessor)
    {
    }
}

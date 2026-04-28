using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Infrastructure.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/publish-channels/catalog")]
[Authorize]
public sealed class PublishChannelCatalogController : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyList<PublishChannelCatalogItemDto>>> List()
    {
        var items = ChannelCatalog.All
            .Select(entry => new PublishChannelCatalogItemDto(
                entry.ChannelKey,
                entry.DisplayName,
                entry.PublishChannelType,
                entry.CredentialKind,
                entry.AllowDraft,
                entry.AllowOnline))
            .ToArray();

        return Ok(ApiResponse<IReadOnlyList<PublishChannelCatalogItemDto>>.Ok(items, HttpContext.TraceIdentifier));
    }
}

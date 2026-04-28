using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/v1/microflow-folders")]
public sealed class MicroflowFoldersController : MicroflowApiControllerBase
{
    private readonly IMicroflowFolderService _folderService;

    public MicroflowFoldersController(
        IMicroflowFolderService folderService,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _folderService = folderService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MicroflowFolderDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MicroflowFolderDto>>>> List(
        [FromQuery] string? workspaceId,
        [FromQuery] string moduleId,
        CancellationToken cancellationToken)
    {
        var result = await _folderService.ListAsync(workspaceId, moduleId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("tree")]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MicroflowFolderTreeNodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MicroflowFolderTreeNodeDto>>>> Tree(
        [FromQuery] string? workspaceId,
        [FromQuery] string moduleId,
        CancellationToken cancellationToken)
    {
        var result = await _folderService.GetTreeAsync(workspaceId, moduleId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowFolderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowFolderDto>>> Create(
        [FromBody] CreateMicroflowFolderRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _folderService.CreateAsync(request, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("{id}/rename")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowFolderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowFolderDto>>> Rename(
        string id,
        [FromBody] RenameMicroflowFolderRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _folderService.RenameAsync(id, request, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("{id}/move")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowFolderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowFolderDto>>> Move(
        string id,
        [FromBody] MoveMicroflowFolderRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _folderService.MoveAsync(id, request, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<DeleteMicroflowResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<DeleteMicroflowResponseDto>>> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        await _folderService.DeleteAsync(id, cancellationToken);
        return MicroflowOk(new DeleteMicroflowResponseDto { Id = id });
    }
}

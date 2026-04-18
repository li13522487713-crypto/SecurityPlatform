using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.System.Abstractions;
using Atlas.Domain.Audit.Entities;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/knowledge-bases")]
public sealed class KnowledgeBasesController : ControllerBase
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly IDocumentService _documentService;
    private readonly IChunkService _chunkService;
    private readonly IRagRetrievalService _ragRetrievalService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<KnowledgeBaseCreateRequest> _kbCreateValidator;
    private readonly IValidator<KnowledgeBaseUpdateRequest> _kbUpdateValidator;
    private readonly IValidator<DocumentCreateRequest> _documentCreateValidator;
    private readonly IValidator<DocumentResegmentRequest> _documentResegmentValidator;
    private readonly IValidator<ChunkCreateRequest> _chunkCreateValidator;
    private readonly IValidator<ChunkUpdateRequest> _chunkUpdateValidator;
    private readonly IValidator<KnowledgeRetrievalTestRequest> _retrievalTestValidator;
    private readonly IAuditWriter _auditWriter;

    public KnowledgeBasesController(
        IKnowledgeBaseService knowledgeBaseService,
        IDocumentService documentService,
        IChunkService chunkService,
        IRagRetrievalService ragRetrievalService,
        IFileStorageService fileStorageService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IAuditWriter auditWriter,
        IValidator<KnowledgeBaseCreateRequest> kbCreateValidator,
        IValidator<KnowledgeBaseUpdateRequest> kbUpdateValidator,
        IValidator<DocumentCreateRequest> documentCreateValidator,
        IValidator<DocumentResegmentRequest> documentResegmentValidator,
        IValidator<ChunkCreateRequest> chunkCreateValidator,
        IValidator<ChunkUpdateRequest> chunkUpdateValidator,
        IValidator<KnowledgeRetrievalTestRequest> retrievalTestValidator)
    {
        _knowledgeBaseService = knowledgeBaseService;
        _documentService = documentService;
        _chunkService = chunkService;
        _ragRetrievalService = ragRetrievalService;
        _fileStorageService = fileStorageService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _kbCreateValidator = kbCreateValidator;
        _kbUpdateValidator = kbUpdateValidator;
        _documentCreateValidator = documentCreateValidator;
        _documentResegmentValidator = documentResegmentValidator;
        _chunkCreateValidator = chunkCreateValidator;
        _chunkUpdateValidator = chunkUpdateValidator;
        _retrievalTestValidator = retrievalTestValidator;
        _auditWriter = auditWriter;
    }

    private async Task WriteKnowledgeAuditAsync(string action, string target, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUserOrThrow();
        await _auditWriter.WriteAsync(
            new AuditRecord(tenantId, user.UserId.ToString(), action, "success", target, null, null),
            cancellationToken);
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeBaseDto>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _knowledgeBaseService.GetPagedAsync(
            tenantId,
            keyword ?? request.Keyword,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeBaseDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<KnowledgeBaseDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _knowledgeBaseService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<KnowledgeBaseDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "KnowledgeBaseNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<KnowledgeBaseDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] KnowledgeBaseCreateRequest request,
        CancellationToken cancellationToken)
    {
        _kbCreateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _knowledgeBaseService.CreateAsync(tenantId, request, cancellationToken);
        await WriteKnowledgeAuditAsync("knowledge_base.create", $"kb:{id}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] KnowledgeBaseUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _kbUpdateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _knowledgeBaseService.UpdateAsync(tenantId, id, request, cancellationToken);
        await WriteKnowledgeAuditAsync("knowledge_base.update", $"kb:{id}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _knowledgeBaseService.DeleteAsync(tenantId, id, cancellationToken);
        await WriteKnowledgeAuditAsync("knowledge_base.delete", $"kb:{id}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/documents")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeDocumentDto>>>> ListDocuments(
        long id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _documentService.ListByKnowledgeBaseAsync(
            tenantId,
            id,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeDocumentDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/documents")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseCreate)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<object>>> CreateDocument(
        long id,
        [FromForm] IFormFile? file,
        [FromForm] long? fileId,
        [FromForm] string? tagsJson,
        [FromForm] string? imageMetadataJson,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actualFileId = fileId;
        if (!actualFileId.HasValue || actualFileId.Value <= 0)
        {
            if (file is null || file.Length <= 0)
            {
                return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ValidationError, ApiResponseLocalizer.T(HttpContext, "KnowledgeBaseImportRequiresFileOrId"), HttpContext.TraceIdentifier));
            }

            var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
            await using var stream = file.OpenReadStream();
            var uploaded = await _fileStorageService.UploadAsync(
                tenantId,
                currentUser.UserId,
                currentUser.Username ?? currentUser.UserId.ToString(),
                file.FileName,
                file.ContentType,
                stream,
                file.Length,
                cancellationToken);
            actualFileId = uploaded.Id;
        }

        var createRequest = new DocumentCreateRequest(actualFileId.Value, tagsJson, imageMetadataJson);
        _documentCreateValidator.ValidateAndThrow(createRequest);
        var documentId = await _documentService.CreateAsync(tenantId, id, createRequest, cancellationToken);
        await WriteKnowledgeAuditAsync("knowledge_document.create", $"kb:{id}/doc:{documentId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = documentId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/documents/{docId:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDocument(
        long id,
        long docId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _documentService.DeleteAsync(tenantId, id, docId, cancellationToken);
        await WriteKnowledgeAuditAsync("knowledge_document.delete", $"kb:{id}/doc:{docId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = docId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/documents/{docId:long}/progress")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<DocumentProgressDto>>> GetDocumentProgress(
        long id,
        long docId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _documentService.GetProgressAsync(tenantId, id, docId, cancellationToken);
        return Ok(ApiResponse<DocumentProgressDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/documents/{docId:long}/resegment")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> ResegmentDocument(
        long id,
        long docId,
        [FromBody] DocumentResegmentRequest? request,
        CancellationToken cancellationToken)
    {
        var effectiveRequest = request ?? new DocumentResegmentRequest();
        _documentResegmentValidator.ValidateAndThrow(effectiveRequest);
        var tenantId = _tenantProvider.GetTenantId();
        await _documentService.ResegmentAsync(tenantId, id, docId, effectiveRequest, cancellationToken);
        await WriteKnowledgeAuditAsync("knowledge_document.resegment", $"kb:{id}/doc:{docId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = docId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/documents/{docId:long}/chunks")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentChunkDto>>>> ListChunks(
        long id,
        long docId,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _chunkService.GetByDocumentAsync(
            tenantId,
            id,
            docId,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<DocumentChunkDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/chunks")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseCreate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateChunk(
        long id,
        [FromBody] ChunkCreateRequest request,
        CancellationToken cancellationToken)
    {
        _chunkCreateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var chunkId = await _chunkService.CreateAsync(tenantId, id, request, cancellationToken);
        await WriteKnowledgeAuditAsync("knowledge_chunk.create", $"kb:{id}/chunk:{chunkId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = chunkId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/chunks/{chunkId:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateChunk(
        long id,
        long chunkId,
        [FromBody] ChunkUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _chunkUpdateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _chunkService.UpdateAsync(tenantId, id, chunkId, request, cancellationToken);
        await WriteKnowledgeAuditAsync("knowledge_chunk.update", $"kb:{id}/chunk:{chunkId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = chunkId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/chunks/{chunkId:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteChunk(
        long id,
        long chunkId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _chunkService.DeleteAsync(tenantId, id, chunkId, cancellationToken);
        await WriteKnowledgeAuditAsync("knowledge_chunk.delete", $"kb:{id}/chunk:{chunkId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = chunkId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/retrieval-test")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RagSearchResult>>>> RetrievalTest(
        long id,
        [FromBody] KnowledgeRetrievalTestRequest request,
        CancellationToken cancellationToken)
    {
        _retrievalTestValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var kbIds = request.KnowledgeBaseIds is { Count: > 0 }
            ? request.KnowledgeBaseIds.Where(x => x > 0).Distinct().ToArray()
            : [id];
        var filter = new RagRetrievalFilter(request.Tags, request.MinScore, request.Offset, request.OwnerFilter, request.MetadataFilter);
        var result = await _ragRetrievalService.SearchAsync(tenantId, kbIds, request.Query.Trim(), request.TopK, filter, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RagSearchResult>>.Ok(result, HttpContext.TraceIdentifier));
    }
}

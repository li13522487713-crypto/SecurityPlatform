using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class VoiceAssetService : IVoiceAssetService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public VoiceAssetService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task<VoiceAssetCreatedDto> CreateAsync(
        TenantId tenantId,
        VoiceAssetCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessException("名称为空。", ErrorCodes.ValidationError);
        }

        var entity = new VoiceAsset(
            tenantId,
            request.Name.Trim(),
            request.Description,
            string.IsNullOrWhiteSpace(request.Language) ? "zh-CN" : request.Language.Trim(),
            string.IsNullOrWhiteSpace(request.Gender) ? "neutral" : request.Gender.Trim(),
            string.IsNullOrWhiteSpace(request.PreviewUrl) ? null : request.PreviewUrl.Trim(),
            LibrarySource.Custom,
            _idGen.NextId());
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return new VoiceAssetCreatedDto(entity.Id);
    }
}

using System.Text.Json;
using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Coze;

/// <summary>
/// 个人设置持久化版（M6.3）。底层用 <see cref="UserSetting"/> KV 表存储，
/// (tenantId, userId, settingKey) 唯一定位一行。跨进程保留偏好。
///
/// 当前 settingKey：
/// - "general"：用户通用偏好（locale / theme / defaultWorkspaceId）
///
/// publish-channels / data-sources 暂仍返回内置常量；接入用户级渠道授权后再持久化。
/// </summary>
public sealed class PersistentMeSettingsService : IMeSettingsService
{
    private const string GeneralKey = "general";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly MeGeneralSettingsDto DefaultGeneral = new(
        Locale: "zh-CN",
        Theme: "light",
        DefaultWorkspaceId: null);

    private static readonly IReadOnlyList<MePublishChannelDto> DefaultChannels = new[]
    {
        new MePublishChannelDto("ch-wechat-personal", "微信个人", "wechat-personal", false),
        new MePublishChannelDto("ch-feishu-personal", "飞书个人", "feishu-personal", false)
    };

    private static readonly IReadOnlyList<MeDataSourceDto> DefaultDataSources = new[]
    {
        new MeDataSourceDto("ds-qdrant", "默认 Qdrant", "qdrant", true),
        new MeDataSourceDto("ds-minio", "默认 MinIO", "minio", true)
    };

    private readonly UserSettingRepository _repository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public PersistentMeSettingsService(
        UserSettingRepository repository,
        IIdGeneratorAccessor idGenerator)
    {
        _repository = repository;
        _idGenerator = idGenerator;
    }

    public async Task<MeGeneralSettingsDto> GetGeneralAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindAsync(tenantId, currentUser.UserId, GeneralKey, cancellationToken);
        if (entity is null)
        {
            return DefaultGeneral;
        }

        return DeserializeGeneral(entity.ValueJson) ?? DefaultGeneral;
    }

    public async Task<MeGeneralSettingsDto> UpdateGeneralAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        MeGeneralSettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.FindAsync(tenantId, currentUser.UserId, GeneralKey, cancellationToken);
        var current = existing is null ? DefaultGeneral : (DeserializeGeneral(existing.ValueJson) ?? DefaultGeneral);
        var next = Apply(current, request);
        var nextJson = JsonSerializer.Serialize(next, JsonOptions);

        if (existing is null)
        {
            var entity = new UserSetting(
                tenantId,
                currentUser.UserId,
                GeneralKey,
                nextJson,
                _idGenerator.NextId());
            await _repository.AddAsync(entity, cancellationToken);
        }
        else
        {
            existing.UpdateValue(nextJson);
            await _repository.UpdateAsync(existing, cancellationToken);
        }

        return next;
    }

    public Task<IReadOnlyList<MePublishChannelDto>> ListPublishChannelsAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(DefaultChannels);
    }

    public Task<IReadOnlyList<MeDataSourceDto>> ListDataSourcesAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(DefaultDataSources);
    }

    public async Task DeleteAccountAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken)
    {
        // M6.3 仍保留"占位删除"语义：仅清空当前用户的所有 UserSetting；
        // 真正的账号注销走 Identity 模块独立流程，避免本批触发安全合规审计变更。
        await _repository.DeleteByUserAsync(tenantId, currentUser.UserId, cancellationToken);
    }

    private static MeGeneralSettingsDto? DeserializeGeneral(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<MeGeneralSettingsDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MeGeneralSettingsDto Apply(MeGeneralSettingsDto current, MeGeneralSettingsUpdateRequest patch)
    {
        return new MeGeneralSettingsDto(
            Locale: NormalizeLocale(patch.Locale ?? current.Locale),
            Theme: NormalizeTheme(patch.Theme ?? current.Theme),
            DefaultWorkspaceId: patch.DefaultWorkspaceId ?? current.DefaultWorkspaceId);
    }

    private static string NormalizeLocale(string locale)
    {
        return locale switch
        {
            "zh-CN" or "en-US" => locale,
            _ => "zh-CN"
        };
    }

    private static string NormalizeTheme(string theme)
    {
        return theme switch
        {
            "light" or "dark" or "system" => theme,
            _ => "light"
        };
    }
}

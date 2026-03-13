using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiPluginService : IAiPluginService
{
    private static readonly HashSet<string> SupportedHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
    };

    private readonly AiPluginRepository _pluginRepository;
    private readonly AiPluginApiRepository _apiRepository;
    private readonly BuiltInPluginMetadataProvider _metadataProvider;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public AiPluginService(
        AiPluginRepository pluginRepository,
        AiPluginApiRepository apiRepository,
        BuiltInPluginMetadataProvider metadataProvider,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _pluginRepository = pluginRepository;
        _apiRepository = apiRepository;
        _metadataProvider = metadataProvider;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<AiPluginListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _pluginRepository.GetPagedAsync(
            tenantId,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);
        return new PagedResult<AiPluginListItem>(
            items.Select(MapListItem).ToList(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<AiPluginDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var plugin = await _pluginRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (plugin is null)
        {
            return null;
        }

        var apis = await _apiRepository.GetByPluginIdAsync(tenantId, id, cancellationToken);
        return MapDetail(plugin, apis);
    }

    public async Task<long> CreateAsync(TenantId tenantId, AiPluginCreateRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        if (await _pluginRepository.ExistsByNameAsync(tenantId, normalizedName, excludeId: null, cancellationToken))
        {
            throw new BusinessException("插件名称已存在。", ErrorCodes.ValidationError);
        }

        ValidateJsonPayload(request.DefinitionJson, "定义配置");
        var plugin = new AiPlugin(
            tenantId,
            normalizedName,
            request.Description?.Trim(),
            request.Icon?.Trim(),
            request.Category?.Trim(),
            request.Type,
            request.DefinitionJson,
            _idGeneratorAccessor.NextId());
        await _pluginRepository.AddAsync(plugin, cancellationToken);
        return plugin.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long id,
        AiPluginUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var plugin = await GetPluginOrThrowAsync(tenantId, id, cancellationToken);
        EnsureEditable(plugin);

        var normalizedName = request.Name.Trim();
        if (await _pluginRepository.ExistsByNameAsync(tenantId, normalizedName, excludeId: id, cancellationToken))
        {
            throw new BusinessException("插件名称已存在。", ErrorCodes.ValidationError);
        }

        ValidateJsonPayload(request.DefinitionJson, "定义配置");
        plugin.Update(
            normalizedName,
            request.Description?.Trim(),
            request.Icon?.Trim(),
            request.Category?.Trim(),
            request.Type,
            request.DefinitionJson);
        await _pluginRepository.UpdateAsync(plugin, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var plugin = await GetPluginOrThrowAsync(tenantId, id, cancellationToken);
        EnsureEditable(plugin);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _apiRepository.DeleteByPluginIdAsync(tenantId, id, cancellationToken);
            await _pluginRepository.DeleteAsync(tenantId, id, cancellationToken);
        }, cancellationToken);
    }

    public async Task PublishAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var plugin = await GetPluginOrThrowAsync(tenantId, id, cancellationToken);
        plugin.Publish();
        await _pluginRepository.UpdateAsync(plugin, cancellationToken);
    }

    public async Task SetLockAsync(TenantId tenantId, long id, bool isLocked, CancellationToken cancellationToken)
    {
        var plugin = await GetPluginOrThrowAsync(tenantId, id, cancellationToken);
        if (isLocked)
        {
            plugin.Lock();
        }
        else
        {
            plugin.Unlock();
        }

        await _pluginRepository.UpdateAsync(plugin, cancellationToken);
    }

    public async Task<AiPluginDebugResult> DebugAsync(
        TenantId tenantId,
        long id,
        AiPluginDebugRequest request,
        CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var plugin = await GetPluginOrThrowAsync(tenantId, id, cancellationToken);
        AiPluginApi? api = null;
        if (request.ApiId.HasValue && request.ApiId.Value > 0)
        {
            api = await _apiRepository.FindByPluginAndIdAsync(tenantId, id, request.ApiId.Value, cancellationToken);
            if (api is null)
            {
                throw new BusinessException("调试接口不存在。", ErrorCodes.NotFound);
            }
        }

        ValidateJsonPayload(request.InputJson, "调试输入");
        var output = new
        {
            pluginId = plugin.Id,
            pluginName = plugin.Name,
            apiId = api?.Id,
            apiName = api?.Name,
            echoedInput = request.InputJson
        };
        watch.Stop();
        return new AiPluginDebugResult(
            true,
            JsonSerializer.Serialize(output),
            null,
            watch.ElapsedMilliseconds);
    }

    public async Task<AiPluginOpenApiImportResult> ImportOpenApiAsync(
        TenantId tenantId,
        long id,
        AiPluginOpenApiImportRequest request,
        CancellationToken cancellationToken)
    {
        var plugin = await GetPluginOrThrowAsync(tenantId, id, cancellationToken);
        EnsureEditable(plugin);

        var openApiNode = JsonNode.Parse(request.OpenApiJson)
            ?? throw new BusinessException("OpenAPI JSON 不能为空。", ErrorCodes.ValidationError);
        var pathsNode = openApiNode["paths"] as JsonObject;
        if (pathsNode is null || pathsNode.Count == 0)
        {
            throw new BusinessException("OpenAPI 缺少 paths 定义。", ErrorCodes.ValidationError);
        }

        var apis = new List<AiPluginApi>();
        foreach (var pathEntry in pathsNode)
        {
            if (pathEntry.Value is not JsonObject methodsNode)
            {
                continue;
            }

            foreach (var methodEntry in methodsNode)
            {
                if (!SupportedHttpMethods.Contains(methodEntry.Key))
                {
                    continue;
                }

                var operationNode = methodEntry.Value as JsonObject;
                var name = operationNode?["summary"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = $"{methodEntry.Key.ToUpperInvariant()} {pathEntry.Key}";
                }

                var description = operationNode?["description"]?.GetValue<string>()?.Trim();
                var requestSchema = operationNode?["requestBody"]?.ToJsonString();
                var responseSchema = operationNode?["responses"]?.ToJsonString();
                apis.Add(new AiPluginApi(
                    tenantId,
                    id,
                    name,
                    description,
                    methodEntry.Key.ToUpperInvariant(),
                    pathEntry.Key,
                    requestSchema,
                    responseSchema,
                    timeoutSeconds: 30,
                    _idGeneratorAccessor.NextId()));
            }
        }

        if (apis.Count == 0)
        {
            throw new BusinessException("OpenAPI 未解析到可导入接口。", ErrorCodes.ValidationError);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            if (request.Overwrite)
            {
                await _apiRepository.DeleteByPluginIdAsync(tenantId, id, cancellationToken);
            }

            await _apiRepository.AddRangeAsync(apis, cancellationToken);
        }, cancellationToken);

        return new AiPluginOpenApiImportResult(apis.Count, apis.Select(x => x.Name).ToArray());
    }

    public async Task<IReadOnlyList<AiPluginBuiltInMetaItem>> GetBuiltInMetadataAsync(CancellationToken cancellationToken)
    {
        var items = await _metadataProvider.GetAllAsync(cancellationToken);
        return items
            .Select(x => new AiPluginBuiltInMetaItem(
                x.Code,
                x.Name,
                x.Description,
                x.Category,
                x.Version,
                x.Tags))
            .ToArray();
    }

    public async Task<IReadOnlyList<AiPluginApiItem>> GetApisAsync(
        TenantId tenantId,
        long pluginId,
        CancellationToken cancellationToken)
    {
        await GetPluginOrThrowAsync(tenantId, pluginId, cancellationToken);
        var apis = await _apiRepository.GetByPluginIdAsync(tenantId, pluginId, cancellationToken);
        return apis.Select(MapApi).ToArray();
    }

    public async Task<long> CreateApiAsync(
        TenantId tenantId,
        long pluginId,
        AiPluginApiCreateRequest request,
        CancellationToken cancellationToken)
    {
        var plugin = await GetPluginOrThrowAsync(tenantId, pluginId, cancellationToken);
        EnsureEditable(plugin);
        EnsureHttpMethod(request.Method);
        ValidateJsonPayload(request.RequestSchemaJson, "请求 Schema");
        ValidateJsonPayload(request.ResponseSchemaJson, "响应 Schema");

        var api = new AiPluginApi(
            tenantId,
            pluginId,
            request.Name.Trim(),
            request.Description?.Trim(),
            request.Method.Trim().ToUpperInvariant(),
            request.Path.Trim(),
            request.RequestSchemaJson,
            request.ResponseSchemaJson,
            request.TimeoutSeconds,
            _idGeneratorAccessor.NextId());
        await _apiRepository.AddAsync(api, cancellationToken);
        return api.Id;
    }

    public async Task UpdateApiAsync(
        TenantId tenantId,
        long pluginId,
        long apiId,
        AiPluginApiUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var plugin = await GetPluginOrThrowAsync(tenantId, pluginId, cancellationToken);
        EnsureEditable(plugin);
        EnsureHttpMethod(request.Method);
        ValidateJsonPayload(request.RequestSchemaJson, "请求 Schema");
        ValidateJsonPayload(request.ResponseSchemaJson, "响应 Schema");

        var api = await _apiRepository.FindByPluginAndIdAsync(tenantId, pluginId, apiId, cancellationToken)
            ?? throw new BusinessException("接口不存在。", ErrorCodes.NotFound);
        api.Update(
            request.Name.Trim(),
            request.Description?.Trim(),
            request.Method.Trim().ToUpperInvariant(),
            request.Path.Trim(),
            request.RequestSchemaJson,
            request.ResponseSchemaJson,
            request.TimeoutSeconds,
            request.IsEnabled);
        await _apiRepository.UpdateAsync(api, cancellationToken);
    }

    public async Task DeleteApiAsync(
        TenantId tenantId,
        long pluginId,
        long apiId,
        CancellationToken cancellationToken)
    {
        var plugin = await GetPluginOrThrowAsync(tenantId, pluginId, cancellationToken);
        EnsureEditable(plugin);
        var api = await _apiRepository.FindByPluginAndIdAsync(tenantId, pluginId, apiId, cancellationToken)
            ?? throw new BusinessException("接口不存在。", ErrorCodes.NotFound);
        await _apiRepository.DeleteAsync(tenantId, api.Id, cancellationToken);
    }

    private async Task<AiPlugin> GetPluginOrThrowAsync(TenantId tenantId, long pluginId, CancellationToken cancellationToken)
    {
        return await _pluginRepository.FindByIdAsync(tenantId, pluginId, cancellationToken)
            ?? throw new BusinessException("插件不存在。", ErrorCodes.NotFound);
    }

    private static void EnsureEditable(AiPlugin plugin)
    {
        if (plugin.IsLocked)
        {
            throw new BusinessException("插件已锁定，不能修改。", ErrorCodes.Forbidden);
        }
    }

    private static void ValidateJsonPayload(string? jsonPayload, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(jsonPayload))
        {
            return;
        }

        try
        {
            JsonNode.Parse(jsonPayload);
        }
        catch (JsonException)
        {
            throw new BusinessException($"{fieldName} 不是合法 JSON。", ErrorCodes.ValidationError);
        }
    }

    private static void EnsureHttpMethod(string method)
    {
        if (!SupportedHttpMethods.Contains(method))
        {
            throw new BusinessException($"不支持的 HTTP 方法：{method}", ErrorCodes.ValidationError);
        }
    }

    private static AiPluginListItem MapListItem(AiPlugin plugin)
        => new(
            plugin.Id,
            plugin.Name,
            plugin.Description,
            plugin.Icon,
            plugin.Category,
            plugin.Type,
            plugin.Status,
            plugin.IsLocked,
            plugin.CreatedAt,
            plugin.UpdatedAt,
            plugin.PublishedAt);

    private static AiPluginDetail MapDetail(AiPlugin plugin, IReadOnlyList<AiPluginApi> apis)
        => new(
            plugin.Id,
            plugin.Name,
            plugin.Description,
            plugin.Icon,
            plugin.Category,
            plugin.Type,
            plugin.Status,
            plugin.DefinitionJson,
            plugin.IsLocked,
            plugin.CreatedAt,
            plugin.UpdatedAt,
            plugin.PublishedAt,
            apis.Select(MapApi).ToArray());

    private static AiPluginApiItem MapApi(AiPluginApi api)
        => new(
            api.Id,
            api.PluginId,
            api.Name,
            api.Description,
            api.Method,
            api.Path,
            api.RequestSchemaJson,
            api.ResponseSchemaJson,
            api.TimeoutSeconds,
            api.IsEnabled,
            api.CreatedAt,
            api.UpdatedAt);
}

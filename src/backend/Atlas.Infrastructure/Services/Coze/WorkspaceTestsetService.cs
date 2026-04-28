using System.Text.Json;
using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Coze;

/// <summary>
/// 持久化版本的 Coze 测试集（PRD 05-4.8）。底层复用现有 EvaluationDataset / EvaluationCase
/// Entity，避免新增重复表。
///
/// Scene 字段被复用为"工作空间 + 工作流"索引：
///   <c>coze-testset:{workspaceId}|{workflowId or ""}</c>
/// 这样既能按 workspaceId 隔离查询，又能反查 testset 所属的 workflowId。
/// </summary>
public sealed class WorkspaceTestsetService : IWorkspaceTestsetService
{
    private const string ScenePrefix = "coze-testset:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly EvaluationDatasetRepository _datasetRepository;
    private readonly EvaluationCaseRepository _caseRepository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public WorkspaceTestsetService(
        EvaluationDatasetRepository datasetRepository,
        EvaluationCaseRepository caseRepository,
        IIdGeneratorAccessor idGenerator)
    {
        _datasetRepository = datasetRepository;
        _caseRepository = caseRepository;
        _idGenerator = idGenerator;
    }

    public async Task<PagedResult<TestsetItemDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);

        var (entities, total) = await _datasetRepository.GetPagedByScenePrefixAsync(
            tenantId,
            BuildScenePrefix(workspaceId),
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);

        // 一次性批量统计 rowCount，避免循环内查库。
        var datasetIds = entities.Select(e => e.Id).ToArray();
        var counts = await _caseRepository.CountByDatasetIdsAsync(tenantId, datasetIds, cancellationToken);

        var items = entities
            .Select(entity =>
            {
                var workflowId = ParseWorkflowId(entity.Scene);
                var rowCount = counts.TryGetValue(entity.Id, out var c) ? c : 0;
                return new TestsetItemDto(
                    Id: entity.Id.ToString(),
                    Name: entity.Name,
                    Description: string.IsNullOrEmpty(entity.Description) ? null : entity.Description,
                    WorkflowId: workflowId,
                    RowCount: rowCount,
                    CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
                    UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
            })
            .ToArray();

        return new PagedResult<TestsetItemDto>(items, total, pageIndex, pageSize);
    }

    public async Task<string> CreateAsync(
        TenantId tenantId,
        string workspaceId,
        TestsetCreateRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = new EvaluationDataset(
            tenantId,
            request.Name.Trim(),
            request.Description?.Trim(),
            BuildSceneToken(workspaceId, request.WorkflowId),
            createdByUserId: 0, // Coze 测试集不强制记录创建者；保持为 0 占位。
            _idGenerator.NextId());

        await _datasetRepository.AddAsync(dataset, cancellationToken);

        // 同步把 rows 写入 EvaluationCase（每行 → 一条 Case）。
        if (request.Rows is { Count: > 0 })
        {
            // 一次性构造批量插入对象，禁止在循环内查库；这里只构造对象，最后一次性 AddAsync。
            // RepositoryBase 暂无批量插入接口，分次 Insertable + ExecuteCommandAsync 也满足"循环内不查库"约束。
            foreach (var row in request.Rows)
            {
                var input = JsonSerializer.Serialize(row, JsonOptions);
                var entity = new EvaluationCase(
                    tenantId,
                    dataset.Id,
                    input,
                    expectedOutput: null,
                    referenceOutput: null,
                    tagsJson: "[]",
                    groundTruthChunkIdsJson: "[]",
                    groundTruthCitationsJson: "[]",
                    _idGenerator.NextId());
                await _caseRepository.AddAsync(entity, cancellationToken);
            }
        }

        return dataset.Id.ToString();
    }

    public async Task<TestsetCasePageDto> ListCaseDataAsync(
        TenantId tenantId,
        string workspaceId,
        string? workflowId,
        string? caseName,
        int pageLimit,
        string? nextToken,
        CancellationToken cancellationToken)
    {
        var normalizedLimit = Math.Clamp(pageLimit <= 0 ? 30 : pageLimit, 1, 50);
        var offset = ParseNextToken(nextToken);

        var datasets = string.IsNullOrWhiteSpace(workflowId)
            ? await _datasetRepository.GetByScenePrefixAsync(
                tenantId,
                BuildScenePrefix(workspaceId),
                cancellationToken)
            : await _datasetRepository.GetBySceneTokenAsync(
                tenantId,
                BuildSceneToken(workspaceId, workflowId.Trim()),
                cancellationToken);

        if (datasets.Count == 0)
        {
            return new TestsetCasePageDto(Array.Empty<TestsetCaseDetailDto>(), false, null);
        }

        var datasetNameMap = datasets
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var cases = await _caseRepository.GetByDatasetIdsAsync(
            tenantId,
            datasets.Select(item => item.Id).ToArray(),
            cancellationToken);

        var matched = cases
            .Where(item => MatchCaseName(item, datasetNameMap, caseName))
            .ToArray();

        if (offset >= matched.Length)
        {
            return new TestsetCasePageDto(Array.Empty<TestsetCaseDetailDto>(), false, null);
        }

        var pageItems = matched
            .Skip(offset)
            .Take(normalizedLimit)
            .Select((item, index) => MapCase(item, datasetNameMap, offset + index == 0))
            .ToArray();

        var hasNext = offset + pageItems.Length < matched.Length;
        var next = hasNext ? (offset + pageItems.Length).ToString() : null;
        return new TestsetCasePageDto(pageItems, hasNext, next);
    }

    private static string BuildScenePrefix(string workspaceId)
    {
        return $"{ScenePrefix}{workspaceId}|";
    }

    private static string BuildSceneToken(string workspaceId, string? workflowId)
    {
        return $"{ScenePrefix}{workspaceId}|{workflowId ?? string.Empty}";
    }

    private static int ParseNextToken(string? nextToken)
    {
        if (string.IsNullOrWhiteSpace(nextToken))
        {
            return 0;
        }

        return int.TryParse(nextToken, out var parsed) && parsed > 0 ? parsed : 0;
    }

    private static bool MatchCaseName(
        EvaluationCase item,
        IReadOnlyDictionary<long, string> datasetNameMap,
        string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        var normalized = keyword.Trim();
        var caseName = ResolveCaseName(item, datasetNameMap);
        return caseName.Contains(normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static TestsetCaseDetailDto MapCase(
        EvaluationCase item,
        IReadOnlyDictionary<long, string> datasetNameMap,
        bool isDefault)
    {
        var caseName = ResolveCaseName(item, datasetNameMap);
        var caseDescription = ResolveCaseDescription(item);
        var caseInput = ResolveCaseInput(item);
        var caseBase = new TestsetCaseBaseDto(
            CaseId: item.Id.ToString(),
            Name: caseName,
            Description: caseDescription,
            Input: caseInput,
            IsDefault: isDefault);
        var createTime = new DateTimeOffset(DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var updateTime = new DateTimeOffset(DateTime.SpecifyKind(item.UpdatedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
        return new TestsetCaseDetailDto(
            CaseBase: caseBase,
            CreatorId: "0",
            CreateTimeInSec: createTime,
            UpdateTimeInSec: updateTime);
    }

    private static string ResolveCaseName(
        EvaluationCase item,
        IReadOnlyDictionary<long, string> datasetNameMap)
    {
        if (TryGetStringFromJson(item.Input, "caseBase", "name", out var nestedName))
        {
            return nestedName;
        }

        if (TryGetStringFromJson(item.Input, "name", out var flatName))
        {
            return flatName;
        }

        if (datasetNameMap.TryGetValue(item.DatasetId, out var datasetName) &&
            !string.IsNullOrWhiteSpace(datasetName))
        {
            return $"{datasetName}-{item.Id}";
        }

        return $"case-{item.Id}";
    }

    private static string? ResolveCaseDescription(EvaluationCase item)
    {
        if (TryGetStringFromJson(item.Input, "caseBase", "description", out var nestedDescription))
        {
            return nestedDescription;
        }

        if (TryGetStringFromJson(item.Input, "description", out var flatDescription))
        {
            return flatDescription;
        }

        return null;
    }

    private static string ResolveCaseInput(EvaluationCase item)
    {
        if (TryGetStringFromJson(item.Input, "caseBase", "input", out var nestedInput))
        {
            return nestedInput;
        }

        if (TryGetStringFromJson(item.Input, "input", out var flatInput))
        {
            return flatInput;
        }

        return item.Input;
    }

    private static bool TryGetStringFromJson(
        string json,
        string key,
        out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty(key, out var target) || target.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var raw = target.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            value = raw;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetStringFromJson(
        string json,
        string parentKey,
        string childKey,
        out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty(parentKey, out var parent) ||
                parent.ValueKind != JsonValueKind.Object ||
                !parent.TryGetProperty(childKey, out var child) ||
                child.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var raw = child.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            value = raw;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ParseWorkflowId(string scene)
    {
        if (string.IsNullOrEmpty(scene) || !scene.StartsWith(ScenePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var idx = scene.IndexOf('|');
        if (idx < 0 || idx + 1 >= scene.Length)
        {
            return null;
        }

        var workflowSegment = scene[(idx + 1)..];
        return string.IsNullOrEmpty(workflowSegment) ? null : workflowSegment;
    }
}

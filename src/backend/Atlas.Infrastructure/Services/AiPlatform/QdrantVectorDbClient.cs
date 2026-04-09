using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class QdrantVectorDbClient : IVectorDbClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<AiPlatformOptions> _optionsMonitor;
    private readonly ISecretRefResolver _secretRefResolver;

    public QdrantVectorDbClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<AiPlatformOptions> options,
        ISecretRefResolver secretRefResolver)
    {
        ProviderName = "qdrant";
        _httpClient = httpClientFactory.CreateClient("AiPlatform");
        _optionsMonitor = options;
        _secretRefResolver = secretRefResolver;
    }

    public string ProviderName { get; }

    public async Task EnsureCollectionAsync(string collectionName, int dimensions, CancellationToken ct = default)
    {
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Vector dimensions must be greater than zero.");
        }

        var safeCollection = GetSafeCollectionName(collectionName);
        using var getRequest = CreateRequest(HttpMethod.Get, $"/collections/{safeCollection}");
        using var getResponse = await _httpClient.SendAsync(getRequest, ct);
        if (getResponse.IsSuccessStatusCode)
        {
            return;
        }

        if (getResponse.StatusCode != HttpStatusCode.NotFound)
        {
            var getError = await getResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Qdrant collection check failed ({(int)getResponse.StatusCode}): {getError}");
        }

        using var createRequest = CreateRequest(HttpMethod.Put, $"/collections/{safeCollection}");
        createRequest.Content = JsonContent.Create(new
        {
            vectors = new
            {
                size = dimensions,
                distance = "Cosine"
            }
        });

        using var createResponse = await _httpClient.SendAsync(createRequest, ct);
        if (createResponse.IsSuccessStatusCode)
        {
            return;
        }

        var createError = await createResponse.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"Qdrant collection create failed ({(int)createResponse.StatusCode}): {createError}");
    }

    public async Task UpsertAsync(string collectionName, IEnumerable<VectorRecord> records, CancellationToken ct = default)
    {
        var materialized = records as VectorRecord[] ?? records.ToArray();
        if (materialized.Length == 0)
        {
            return;
        }

        var safeCollection = GetSafeCollectionName(collectionName);
        var dimensions = materialized[0].Vector.Length;
        await EnsureCollectionAsync(safeCollection, dimensions, ct);

        var points = materialized.Select(record => new
        {
            id = record.Id,
            vector = record.Vector,
            payload = new
            {
                content = record.Content,
                metadata = record.Metadata
            }
        }).ToArray();

        using var request = CreateRequest(HttpMethod.Put, $"/collections/{safeCollection}/points?wait=true");
        request.Content = JsonContent.Create(new { points });
        using var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"Qdrant upsert failed ({(int)response.StatusCode}): {error}");
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (queryVector.Length == 0)
        {
            throw new ArgumentException("Query vector cannot be empty.", nameof(queryVector));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "topK must be greater than zero.");
        }

        var safeCollection = GetSafeCollectionName(collectionName);
        using var request = CreateRequest(HttpMethod.Post, $"/collections/{safeCollection}/points/search");
        request.Content = JsonContent.Create(new
        {
            vector = queryVector,
            limit = topK,
            with_payload = true,
            with_vector = false
        });

        using var response = await _httpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Qdrant collection '{safeCollection}' not found.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Qdrant search failed ({(int)response.StatusCode}): {error}");
        }

        var body = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(JsonOptions, ct);
        if (body?.Result is null || body.Result.Count == 0)
        {
            return Array.Empty<VectorSearchResult>();
        }

        return body.Result.Select(item =>
            new VectorSearchResult(
                ResolveId(item.Id),
                ExtractContent(item.Payload),
                item.Score,
                ExtractMetadata(item.Payload))).ToArray();
    }

    public async Task DeleteAsync(string collectionName, IEnumerable<string> ids, CancellationToken ct = default)
    {
        var idList = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (idList.Length == 0)
        {
            return;
        }

        var safeCollection = GetSafeCollectionName(collectionName);
        using var request = CreateRequest(HttpMethod.Post, $"/collections/{safeCollection}/points/delete?wait=true");
        request.Content = JsonContent.Create(new { points = idList });
        using var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Qdrant collection '{safeCollection}' not found.");
        }

        var error = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"Qdrant delete failed ({(int)response.StatusCode}): {error}");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var options = _optionsMonitor.CurrentValue;
        var baseUrl = options.VectorDb.QdrantUrl.TrimEnd('/');
        var apiKey = _secretRefResolver.Resolve(options.VectorDb.QdrantApiKey);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("AiPlatform:VectorDb:QdrantUrl 未配置。");
        }

        var request = new HttpRequestMessage(method, $"{baseUrl}{path}");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
        }

        return request;
    }

    private static string GetSafeCollectionName(string collectionName)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentException("Collection name cannot be empty.", nameof(collectionName));
        }

        var trimmed = collectionName.Trim();
        var isSafe = trimmed.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-');
        if (!isSafe)
        {
            throw new ArgumentException(
                "Collection name only allows letters, numbers, underscore and hyphen.",
                nameof(collectionName));
        }

        return trimmed.ToLowerInvariant();
    }

    private static string ResolveId(JsonElement id)
    {
        return id.ValueKind switch
        {
            JsonValueKind.String => id.GetString() ?? string.Empty,
            JsonValueKind.Number => id.GetInt64().ToString(),
            _ => string.Empty
        };
    }

    private static string ExtractContent(Dictionary<string, JsonElement>? payload)
    {
        if (payload is null)
        {
            return string.Empty;
        }

        if (payload.TryGetValue("content", out var content) && content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<string, string>? ExtractMetadata(Dictionary<string, JsonElement>? payload)
    {
        if (payload is null)
        {
            return null;
        }

        if (!payload.TryGetValue("metadata", out var metadataNode) || metadataNode.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in metadataNode.EnumerateObject())
        {
            metadata[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                _ => property.Value.ToString()
            };
        }

        return metadata.Count == 0 ? null : metadata;
    }

    private sealed record QdrantSearchResponse(
        List<QdrantSearchPoint>? Result);

    private sealed record QdrantSearchPoint(
        JsonElement Id,
        float Score,
        Dictionary<string, JsonElement>? Payload);
}

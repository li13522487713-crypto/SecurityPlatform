using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.Integration;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Integration;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class WebhookService : IWebhookService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGen,
        ITenantProvider tenantProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookService> logger)
    {
        _db = db;
        _idGen = idGen;
        _tenantProvider = tenantProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        return await _db.Queryable<WebhookSubscription>()
            .Where(s => s.TenantIdValue == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WebhookSubscription?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        return await _db.Queryable<WebhookSubscription>()
            .Where(s => s.Id == id && s.TenantIdValue == tenantId)
            .FirstAsync(cancellationToken);
    }

    public async Task<long> CreateAsync(CreateWebhookRequest request, CancellationToken cancellationToken)
    {
        var subscription = new WebhookSubscription(_tenantProvider.TenantId, _idGen.Generator.NextId())
        {
            Name = request.Name,
            EventTypes = JsonSerializer.Serialize(request.EventTypes),
            TargetUrl = request.TargetUrl,
            Secret = request.Secret,
            Headers = request.Headers is { Count: > 0 } ? JsonSerializer.Serialize(request.Headers) : null,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _db.Insertable(subscription).ExecuteCommandAsync(cancellationToken);
        return subscription.Id;
    }

    public async Task UpdateAsync(long id, UpdateWebhookRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        var headersJson = request.Headers != null && request.Headers.Count > 0
            ? JsonSerializer.Serialize(request.Headers)
            : null;
        await _db.Updateable<WebhookSubscription>()
            .SetColumns(s => new WebhookSubscription
            {
                Name = request.Name,
                EventTypes = JsonSerializer.Serialize(request.EventTypes),
                TargetUrl = request.TargetUrl,
                IsActive = request.IsActive,
                Headers = headersJson
            })
            .Where(s => s.Id == id && s.TenantIdValue == tenantId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        await _db.Deleteable<WebhookSubscription>()
            .Where(s => s.Id == id && s.TenantIdValue == tenantId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookDeliveryLog>> GetDeliveriesAsync(
        long subscriptionId, int pageSize, CancellationToken cancellationToken)
    {
        return await _db.Queryable<WebhookDeliveryLog>()
            .Where(l => l.SubscriptionId == subscriptionId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task DispatchAsync(string eventType, string payload, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        var subscriptions = await _db.Queryable<WebhookSubscription>()
            .Where(s => s.IsActive && s.TenantIdValue == tenantId)
            .ToListAsync(cancellationToken);

        var matching = subscriptions.Where(s =>
        {
            var types = JsonSerializer.Deserialize<List<string>>(s.EventTypes) ?? [];
            return types.Contains(eventType) || types.Contains("*");
        }).ToList();

        var deliveryTasks = matching.Select(s => DeliverAsync(s, eventType, payload, cancellationToken));
        await Task.WhenAll(deliveryTasks);
    }

    public async Task TestDeliveryAsync(long subscriptionId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        var subscription = await _db.Queryable<WebhookSubscription>()
            .Where(s => s.Id == subscriptionId && s.TenantIdValue == tenantId)
            .FirstAsync(cancellationToken);

        if (subscription is null) return;

        var testPayload = JsonSerializer.Serialize(new
        {
            eventType = "webhook.test",
            timestamp = DateTimeOffset.UtcNow,
            message = "Atlas Webhook test delivery"
        });

        await DeliverAsync(subscription, "webhook.test", testPayload, cancellationToken);
    }

    private async Task DeliverAsync(
        WebhookSubscription subscription,
        string eventType,
        string payload,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var log = new WebhookDeliveryLog
        {
            Id = _idGen.Generator.NextId(),
            SubscriptionId = subscription.Id,
            EventType = eventType,
            Payload = payload,
            CreatedAt = startedAt
        };

        try
        {
            var client = _httpClientFactory.CreateClient("Webhook");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // HMAC-SHA256 签名
            var signature = ComputeHmacSignature(payload, subscription.Secret);
            content.Headers.Add("X-Atlas-Signature", signature);
            content.Headers.Add("X-Atlas-Event", eventType);

            // 自定义请求头
            if (!string.IsNullOrEmpty(subscription.Headers))
            {
                var extraHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(subscription.Headers);
                if (extraHeaders is not null)
                {
                    foreach (var (k, v) in extraHeaders)
                    {
                        content.Headers.TryAddWithoutValidation(k, v);
                    }
                }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.PostAsync(subscription.TargetUrl, content, cancellationToken);
            sw.Stop();

            log.ResponseCode = (int)response.StatusCode;
            log.ResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            log.DurationMs = (int)sw.ElapsedMilliseconds;
            log.Success = response.IsSuccessStatusCode;

            // 更新最后触发时间
            await _db.Updateable<WebhookSubscription>()
                .SetColumns(s => new WebhookSubscription { LastTriggeredAt = DateTimeOffset.UtcNow })
                .Where(s => s.Id == subscription.Id)
                .ExecuteCommandAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            log.Success = false;
            log.ErrorMessage = ex.Message;
            log.DurationMs = (int)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
            _logger.LogWarning(ex, "Webhook delivery failed for subscription {Id}", subscription.Id);
        }
        finally
        {
            await _db.Insertable(log).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static string ComputeHmacSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

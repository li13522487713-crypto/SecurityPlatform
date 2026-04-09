using System.Text.Json;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class CapabilityRegistry : ICapabilityRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyList<CapabilityManifestItem> CodeDefaults =
    [
        new CapabilityManifestItem(
            CapabilityKey: "organization",
            Title: "Organization",
            Category: "core",
            HostModes: ["platform", "app"],
            PlatformRoute: "/apps/{appId}/capabilities/organization",
            AppRoute: "/apps/{appKey}/capabilities/organization",
            RequiredPermissions: ["departments:view", "positions:view", "projects:view"],
            Navigation: new CapabilityNavigationSuggestion("organization", 100),
            SupportsExposure: true,
            SupportedCommands: ["organization.sync", "organization.replace-structure"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "agent",
            Title: "Agent",
            Category: "ai",
            HostModes: ["platform", "app"],
            PlatformRoute: "/apps/{appId}/capabilities/agent",
            AppRoute: "/apps/{appKey}/capabilities/agent",
            RequiredPermissions: ["ai:agents:view"],
            Navigation: new CapabilityNavigationSuggestion("ai", 200),
            SupportsExposure: true,
            SupportedCommands: ["agent.publish", "agent.debug"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "workflow",
            Title: "Workflow",
            Category: "ai",
            HostModes: ["platform", "app"],
            PlatformRoute: "/apps",
            AppRoute: "/apps/{appKey}/workflows",
            RequiredPermissions: ["workflows:view"],
            Navigation: new CapabilityNavigationSuggestion("workflow", 300),
            SupportsExposure: true,
            SupportedCommands: ["workflow.deploy", "workflow.resume"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "appbridge-online-apps",
            Title: "AppBridge Online Apps",
            Category: "appbridge",
            HostModes: ["platform"],
            PlatformRoute: "/console/appbridge/online-apps",
            AppRoute: null,
            RequiredPermissions: ["apps:view"],
            Navigation: new CapabilityNavigationSuggestion("monitor", 410),
            SupportsExposure: true,
            SupportedCommands: ["appbridge.federated.register", "appbridge.federated.heartbeat"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "appbridge-command-center",
            Title: "AppBridge Command Center",
            Category: "appbridge",
            HostModes: ["platform"],
            PlatformRoute: "/console/appbridge/command-center",
            AppRoute: null,
            RequiredPermissions: ["apps:update"],
            Navigation: new CapabilityNavigationSuggestion("monitor", 420),
            SupportsExposure: true,
            SupportedCommands: ["appbridge.command.dispatch", "appbridge.command.ack"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "appbridge-data-browser",
            Title: "AppBridge Data Browser",
            Category: "appbridge",
            HostModes: ["platform"],
            PlatformRoute: "/console/appbridge/data-browser",
            AppRoute: null,
            RequiredPermissions: ["apps:view"],
            Navigation: new CapabilityNavigationSuggestion("monitor", 430),
            SupportsExposure: true,
            SupportedCommands: ["appbridge.exposure.query"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "knowledge-base",
            Title: "Knowledge Base",
            Category: "ai",
            HostModes: ["platform", "app"],
            PlatformRoute: "/ai/knowledge-bases",
            AppRoute: "/apps/{appKey}/knowledge-bases",
            RequiredPermissions: ["ai:knowledge:view"],
            Navigation: new CapabilityNavigationSuggestion("ai", 210),
            SupportsExposure: true,
            SupportedCommands: ["knowledge.index", "knowledge.rebuild"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "ai-evaluation",
            Title: "AI Evaluation",
            Category: "ai",
            HostModes: ["platform", "app"],
            PlatformRoute: "/ai/evaluations",
            AppRoute: "/apps/{appKey}/evaluations",
            RequiredPermissions: ["ai:evaluations:view"],
            Navigation: new CapabilityNavigationSuggestion("ai", 220),
            SupportsExposure: true,
            SupportedCommands: ["evaluation.run", "evaluation.compare"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "model-config",
            Title: "Model Config",
            Category: "ai",
            HostModes: ["platform", "app"],
            PlatformRoute: "/ai/model-configs",
            AppRoute: "/apps/{appKey}/model-configs",
            RequiredPermissions: ["ai:model-config:view"],
            Navigation: new CapabilityNavigationSuggestion("ai", 230),
            SupportsExposure: false,
            SupportedCommands: ["model-config.test", "model-config.publish"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "dynamic-data",
            Title: "Dynamic Data",
            Category: "data",
            HostModes: ["platform", "app"],
            PlatformRoute: "/apps/{appId}/data",
            AppRoute: "/apps/{appKey}/data",
            RequiredPermissions: ["dynamic-tables:view"],
            Navigation: new CapabilityNavigationSuggestion("data", 300),
            SupportsExposure: true,
            SupportedCommands: ["dynamic-table.publish", "dynamic-table.migrate"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "workflow-designer",
            Title: "Workflow Designer",
            Category: "workflow",
            HostModes: ["platform", "app"],
            PlatformRoute: "/apps/{appId}/workflows",
            AppRoute: "/apps/{appKey}/workflows",
            RequiredPermissions: ["workflows:view"],
            Navigation: new CapabilityNavigationSuggestion("workflow", 310),
            SupportsExposure: true,
            SupportedCommands: ["workflow.publish", "workflow.rollback"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "logic-flow",
            Title: "Logic Flow",
            Category: "workflow",
            HostModes: ["platform", "app"],
            PlatformRoute: "/apps/{appId}/logic-flow",
            AppRoute: "/apps/{appKey}/logic-flow",
            RequiredPermissions: ["logic-flow:view"],
            Navigation: new CapabilityNavigationSuggestion("workflow", 320),
            SupportsExposure: true,
            SupportedCommands: ["logic-flow.publish", "logic-flow.debug"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "connector-online-apps",
            Title: "Connector Online Apps",
            Category: "connectors",
            HostModes: ["platform"],
            PlatformRoute: "/console/connectors/online-apps",
            AppRoute: null,
            RequiredPermissions: ["apps:view"],
            Navigation: new CapabilityNavigationSuggestion("connectors", 330),
            SupportsExposure: true,
            SupportedCommands: ["connector.register", "connector.heartbeat"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "connector-command-center",
            Title: "Connector Command Center",
            Category: "connectors",
            HostModes: ["platform"],
            PlatformRoute: "/console/connectors/command-center",
            AppRoute: null,
            RequiredPermissions: ["apps:update"],
            Navigation: new CapabilityNavigationSuggestion("connectors", 340),
            SupportsExposure: true,
            SupportedCommands: ["connector.command.dispatch"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "runtime-context",
            Title: "Runtime Context",
            Category: "runtime",
            HostModes: ["platform"],
            PlatformRoute: "/console/runtime-contexts",
            AppRoute: null,
            RequiredPermissions: ["runtime-context:view"],
            Navigation: new CapabilityNavigationSuggestion("runtime", 350),
            SupportsExposure: true,
            SupportedCommands: ["runtime-context.refresh"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "runtime-execution",
            Title: "Runtime Execution",
            Category: "runtime",
            HostModes: ["platform"],
            PlatformRoute: "/console/runtime-executions",
            AppRoute: null,
            RequiredPermissions: ["runtime-execution:view"],
            Navigation: new CapabilityNavigationSuggestion("runtime", 360),
            SupportsExposure: true,
            SupportedCommands: ["runtime-execution.cancel", "runtime-execution.retry"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "release-center",
            Title: "Release Center",
            Category: "runtime",
            HostModes: ["platform"],
            PlatformRoute: "/console/releases",
            AppRoute: null,
            RequiredPermissions: ["apps:release:view"],
            Navigation: new CapabilityNavigationSuggestion("runtime", 370),
            SupportsExposure: true,
            SupportedCommands: ["release.publish", "release.rollback"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "resource-center",
            Title: "Resource Center",
            Category: "runtime",
            HostModes: ["platform"],
            PlatformRoute: "/console/resources",
            AppRoute: null,
            RequiredPermissions: ["apps:view"],
            Navigation: new CapabilityNavigationSuggestion("runtime", 380),
            SupportsExposure: true,
            SupportedCommands: ["resource.repair"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "tenant-application",
            Title: "Tenant Application",
            Category: "core",
            HostModes: ["platform"],
            PlatformRoute: "/console/tenant-applications",
            AppRoute: null,
            RequiredPermissions: ["apps:view"],
            Navigation: new CapabilityNavigationSuggestion("core", 390),
            SupportsExposure: false,
            SupportedCommands: ["tenant-app.open", "tenant-app.disable"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "application-catalog",
            Title: "Application Catalog",
            Category: "core",
            HostModes: ["platform"],
            PlatformRoute: "/console/catalog",
            AppRoute: null,
            RequiredPermissions: ["apps:view"],
            Navigation: new CapabilityNavigationSuggestion("core", 395),
            SupportsExposure: false,
            SupportedCommands: ["catalog.publish", "catalog.archive"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "audit-log",
            Title: "Audit Log",
            Category: "security",
            HostModes: ["platform"],
            PlatformRoute: "/audit",
            AppRoute: null,
            RequiredPermissions: ["audit:view"],
            Navigation: new CapabilityNavigationSuggestion("security", 440),
            SupportsExposure: false,
            SupportedCommands: ["audit.export"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "alert-center",
            Title: "Alert Center",
            Category: "security",
            HostModes: ["platform"],
            PlatformRoute: "/alert",
            AppRoute: null,
            RequiredPermissions: ["alerts:view"],
            Navigation: new CapabilityNavigationSuggestion("security", 450),
            SupportsExposure: false,
            SupportedCommands: ["alert.ack", "alert.close"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "asset-center",
            Title: "Asset Center",
            Category: "security",
            HostModes: ["platform"],
            PlatformRoute: "/assets",
            AppRoute: null,
            RequiredPermissions: ["assets:view"],
            Navigation: new CapabilityNavigationSuggestion("security", 460),
            SupportsExposure: false,
            SupportedCommands: ["asset.scan", "asset.export"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "migration-governance",
            Title: "Migration Governance",
            Category: "governance",
            HostModes: ["platform"],
            PlatformRoute: "/console/migration-governance",
            AppRoute: null,
            RequiredPermissions: ["apps:migrate:view"],
            Navigation: new CapabilityNavigationSuggestion("governance", 470),
            SupportsExposure: false,
            SupportedCommands: ["migration.check", "migration.repair"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "system-settings",
            Title: "System Settings",
            Category: "settings",
            HostModes: ["platform"],
            PlatformRoute: "/settings/system/configs",
            AppRoute: null,
            RequiredPermissions: ["system-config:view"],
            Navigation: new CapabilityNavigationSuggestion("settings", 480),
            SupportsExposure: false,
            SupportedCommands: ["system-config.update"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "project-management",
            Title: "Project Management",
            Category: "settings",
            HostModes: ["platform"],
            PlatformRoute: "/settings/projects",
            AppRoute: null,
            RequiredPermissions: ["projects:view"],
            Navigation: new CapabilityNavigationSuggestion("settings", 490),
            SupportsExposure: false,
            SupportedCommands: ["project.create", "project.archive"],
            IsEnabled: true),
        new CapabilityManifestItem(
            CapabilityKey: "ai-marketplace",
            Title: "AI Marketplace",
            Category: "ai",
            HostModes: ["platform", "app"],
            PlatformRoute: "/ai/marketplace",
            AppRoute: "/apps/{appKey}/marketplace",
            RequiredPermissions: ["ai:marketplace:view"],
            Navigation: new CapabilityNavigationSuggestion("ai", 500),
            SupportsExposure: false,
            SupportedCommands: ["marketplace.install", "marketplace.publish"],
            IsEnabled: true),
    ];

    private readonly ISqlSugarClient _db;

    public CapabilityRegistry(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CapabilityManifestItem>> GetAllAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var merged = CodeDefaults.ToDictionary(item => item.CapabilityKey, StringComparer.OrdinalIgnoreCase);
        var dbRows = await QueryOverridesAsync(tenantId, cancellationToken);
        foreach (var row in dbRows)
        {
            var item = MapEntity(row);
            if (!item.IsEnabled)
            {
                merged.Remove(item.CapabilityKey);
                continue;
            }

            merged[item.CapabilityKey] = item;
        }

        return merged.Values
            .OrderBy(item => item.Navigation.Group ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Navigation.Order ?? int.MaxValue)
            .ThenBy(item => item.CapabilityKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<CapabilityManifestItem?> GetByKeyAsync(
        TenantId tenantId,
        string capabilityKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(capabilityKey))
        {
            return null;
        }

        var all = await GetAllAsync(tenantId, cancellationToken);
        return all.FirstOrDefault(item => string.Equals(item.CapabilityKey, capabilityKey, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<CapabilityManifest>> QueryOverridesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _db.Queryable<CapabilityManifest>()
                .Where(item => item.TenantIdValue == tenantId.Value)
                .ToListAsync(cancellationToken);
        }
        catch
        {
            return [];
        }
    }

    private static CapabilityManifestItem MapEntity(CapabilityManifest entity)
    {
        return new CapabilityManifestItem(
            CapabilityKey: entity.CapabilityKey,
            Title: entity.Title,
            Category: entity.Category,
            HostModes: ParseStringArray(entity.HostModesJson),
            PlatformRoute: NullIfWhiteSpace(entity.PlatformRoute),
            AppRoute: NullIfWhiteSpace(entity.AppRoute),
            RequiredPermissions: ParseStringArray(entity.RequiredPermissionsJson),
            Navigation: ParseNavigation(entity.NavigationJson),
            SupportsExposure: entity.SupportsExposure,
            SupportedCommands: ParseStringArray(entity.SupportedCommandsJson),
            IsEnabled: entity.IsEnabled);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static IReadOnlyList<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return values?
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static CapabilityNavigationSuggestion ParseNavigation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CapabilityNavigationSuggestion(null, null);
        }

        try
        {
            var node = JsonSerializer.Deserialize<CapabilityNavigationSuggestion>(json, JsonOptions);
            return node ?? new CapabilityNavigationSuggestion(null, null);
        }
        catch
        {
            return new CapabilityNavigationSuggestion(null, null);
        }
    }
}

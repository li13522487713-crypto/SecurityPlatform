using System.Linq;
using Atlas.Application.Identity;

namespace Atlas.SecurityPlatform.Tests.Identity;

public sealed class AppPermissionSeedCatalogTests
{
    [Fact]
    public void AllPermissionCodes_ShouldContain_ConversationMarketplaceAndWorkspaceCapabilities()
    {
        var allPermissions = AppPermissionSeedCatalog.AllPermissionCodes;

        Assert.Contains(PermissionCodes.ConversationView, allPermissions);
        Assert.Contains(PermissionCodes.ConversationCreate, allPermissions);
        Assert.Contains(PermissionCodes.ConversationDelete, allPermissions);
        Assert.Contains(PermissionCodes.AiMarketplaceCreate, allPermissions);
        Assert.Contains(PermissionCodes.AiMarketplaceUpdate, allPermissions);
        Assert.Contains(PermissionCodes.AiMarketplaceDelete, allPermissions);
        Assert.Contains(PermissionCodes.AiMarketplacePublish, allPermissions);
        Assert.Contains(PermissionCodes.AiWorkspaceView, allPermissions);
        Assert.Contains(PermissionCodes.AiWorkspaceUpdate, allPermissions);
    }

    [Fact]
    public void SecurityAdmin_ShouldInclude_ModelConversationWorkflowAndMarketplaceCapabilities()
    {
        var granted = AppPermissionSeedCatalog.GetPermissionCodesForRole("SecurityAdmin");

        Assert.Contains(PermissionCodes.ModelConfigView, granted);
        Assert.Contains(PermissionCodes.ModelConfigCreate, granted);
        Assert.Contains(PermissionCodes.ConversationCreate, granted);
        Assert.Contains(PermissionCodes.AiWorkflowExecute, granted);
        Assert.Contains(PermissionCodes.WorkflowDesign, granted);
        Assert.Contains(PermissionCodes.AiMarketplacePublish, granted);
        Assert.Contains(PermissionCodes.AiWorkspaceView, granted);
    }

    [Fact]
    public void AppAdmin_ShouldMapTo_AllPermissionCodes()
    {
        var granted = AppPermissionSeedCatalog.GetPermissionCodesForRole("AppAdmin");

        Assert.Equal(
            AppPermissionSeedCatalog.AllPermissionCodes.OrderBy(code => code, StringComparer.OrdinalIgnoreCase),
            granted.OrderBy(code => code, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolvePermissionSeed_ShouldReturn_MetadataForKnownSeed()
    {
        var seed = AppPermissionSeedCatalog.ResolvePermissionSeed(PermissionCodes.ConversationCreate);

        Assert.Equal(PermissionCodes.ConversationCreate, seed.Code);
        Assert.Equal("Conversation Create", seed.Name);
        Assert.Equal("Api", seed.Type);
    }
}

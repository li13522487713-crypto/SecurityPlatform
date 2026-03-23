using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.Application.Identity;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Alert.Entities;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Assets.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Domain.Plugins;
using Atlas.Domain.Events;
using Atlas.Domain.Workflow.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BootstrapAdminOptions _bootstrapOptions;
    private readonly PasswordPolicyOptions _passwordPolicy;
    private readonly DatabaseEncryptionOptions _encryptionOptions;
    private readonly DatabaseInitializerOptions _initializerOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;

    public DatabaseInitializerHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<BootstrapAdminOptions> bootstrapOptions,
        IOptions<PasswordPolicyOptions> passwordPolicy,
        IOptions<DatabaseEncryptionOptions> encryptionOptions,
        IOptions<DatabaseInitializerOptions> initializerOptions,
        IHostEnvironment environment,
        ILogger<DatabaseInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _bootstrapOptions = bootstrapOptions.Value;
        _passwordPolicy = passwordPolicy.Value;
        _encryptionOptions = encryptionOptions.Value;
        _initializerOptions = initializerOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_encryptionOptions.Enabled && string.IsNullOrWhiteSpace(_encryptionOptions.Key))
        {
            if (_environment.IsProduction())
            {
                // 生产环境：数据库加密已启用但密钥未配置，必须阻止启动
                throw new InvalidOperationException(
                    "生产环境已启用数据库加密（Database:Encryption:Enabled=true）但未配置密钥（Database:Encryption:Key）。" +
                    "请通过环境变量 Database__Encryption__Key 提供密钥，或将 Database:Encryption:Enabled 设为 false。");
            }

            // 非生产环境：输出警告并降级为不加密，不阻止启动
            _logger.LogWarning(
                "[DatabaseInitializer] Database:Encryption:Enabled=true 但 Key 未配置，" +
                "非生产环境下自动降级为不加密运行。若为误配置，请检查环境变量 Database__Encryption__Enabled。");
        }


        using var scope = _scopeFactory.CreateScope();
        var appContextAccessor = scope.ServiceProvider.GetRequiredService<IAppContextAccessor>();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        // 关键兼容迁移：平台管理员标记字段缺失会导致登录链路失败，需始终检查并补齐。
        await EnsureUserAccountSchemaAsync(db, cancellationToken);
        await EnsureAppMembershipSchemaAsync(db, cancellationToken);

        // Schema 迁移检查（兼容历史版本字段结构）
        if (!_initializerOptions.SkipSchemaMigrations)
        {
            _logger.LogInformation("[DatabaseInitializer] 开始执行 Schema 迁移检查...");
            await EnsureAuthSessionSchemaAsync(db, cancellationToken);
            await EnsureRefreshTokenSchemaAsync(db, cancellationToken);
            await EnsureApprovalSchemaAsync(db, cancellationToken);
            await EnsureLowCodeAppSchemaAsync(db, cancellationToken);
            await EnsureTenantDataSourceSchemaAsync(db, cancellationToken);
            await EnsureProductizationSchemaAsync(db, cancellationToken);
            await EnsureWorkflowExecutionSchemaAsync(db, cancellationToken);
            await EnsureAiPluginSchemaAsync(db, cancellationToken);
            await EnsureAiMemorySchemaAsync(db, cancellationToken);
        }
        else
        {
            _logger.LogInformation("[DatabaseInitializer] 已跳过 Schema 迁移检查（DatabaseInitializer:SkipSchemaMigrations=true）");
        }

        // Schema 初始化（CodeFirst.InitTables）
        if (!_initializerOptions.SkipSchemaInit)
        {
            _logger.LogInformation("[DatabaseInitializer] 开始执行 Schema 初始化（CodeFirst.InitTables）...");
            db.CodeFirst.InitTables(
        typeof(UserAccount),
        typeof(Role),
        typeof(Permission),
        typeof(UserRole),
        typeof(RolePermission),
        typeof(RoleDept),
        typeof(Department),
        typeof(Position),
        typeof(UserDepartment),
        typeof(UserPosition),
        typeof(PasswordHistory),
            typeof(Menu),
            typeof(AppConfig),
            typeof(Project),
            typeof(ProjectUser),
            typeof(ProjectDepartment),
            typeof(ProjectPosition),
            typeof(RoleMenu),
            typeof(TableView),
            typeof(UserTableViewDefault),
            typeof(IdempotencyRecord),
            typeof(AuditRecord),
            typeof(Asset),
            typeof(AlertRecord),
            typeof(ModelConfig),
            typeof(Agent),
            typeof(AgentKnowledgeLink),
            typeof(AgentPluginBinding),
            typeof(Conversation),
            typeof(ChatMessage),
            typeof(ShortTermMemory),
            typeof(LongTermMemory),
            typeof(KnowledgeBase),
            typeof(KnowledgeDocument),
            typeof(DocumentChunk),
            typeof(AiWorkflowDefinition),
            typeof(AiDatabase),
            typeof(AiDatabaseRecord),
            typeof(AiDatabaseImportTask),
            typeof(AiVariable),
            typeof(AiPlugin),
            typeof(AiPluginApi),
            typeof(AiApp),
            typeof(AiAppPublishRecord),
            typeof(AiAppResourceCopyTask),
            typeof(AiPromptTemplate),
            typeof(AiProductCategory),
            typeof(AiMarketplaceProduct),
            typeof(AiMarketplaceFavorite),
            typeof(AiRecentEdit),
            typeof(AiWorkspace),
            typeof(AiShortcutCommand),
            typeof(AiBotPopupInfo),
            typeof(PersonalAccessToken),
            typeof(AuthSession),
            typeof(RefreshToken),
            typeof(ApprovalFlowDefinition),
            typeof(ApprovalFlowDefinitionVersion),
            typeof(ApprovalWritebackFailure),
            typeof(ApprovalProcessInstance),
            typeof(ApprovalTask),
            typeof(ApprovalHistoryEvent),
            typeof(ApprovalDepartmentLeader),
            typeof(ApprovalProcessVariable),
            typeof(ApprovalTaskTransfer),
            typeof(ApprovalTaskAssigneeChange),
            typeof(ApprovalNodeExecution),
            typeof(ApprovalOperationRecord),
            typeof(ApprovalFlowButtonConfig),
            typeof(ApprovalTimeoutReminder),
            typeof(ApprovalAgentConfig),
            typeof(ApprovalCopyRecord),
            typeof(ApprovalExternalCallbackRecord),
            typeof(ApprovalParallelToken),
            typeof(ApprovalTimerJob),
            typeof(ApprovalTriggerJob),
            typeof(DynamicTable),
            typeof(DynamicField),
            typeof(DynamicIndex),
            typeof(DynamicRelation),
            typeof(FieldPermission),
            typeof(MigrationRecord),
            typeof(LowCodeApp),
            typeof(AppEntityAlias),
            typeof(LowCodePage),
            typeof(LowCodePageVersion),
            typeof(LowCodeEnvironment),
            typeof(FormDefinition),
            typeof(DashboardDefinition),
            typeof(ReportDefinition),
            typeof(DataSourceDefinition),
            // Workflow entities
            typeof(PersistedWorkflow),
            typeof(PersistedExecutionPointer),
            typeof(PersistedEvent),
            typeof(PersistedSubscription),
            // Workflow V2 entities (Coze-style DAG engine)
            typeof(WorkflowMeta),
            typeof(WorkflowDraft),
            typeof(WorkflowVersion),
            typeof(WorkflowExecution),
            typeof(WorkflowNodeExecution),
            // System module
            typeof(DictType),
            typeof(DictData),
            typeof(SystemConfig),
            typeof(LoginLog),
            typeof(Notification),
            typeof(UserNotification),
            typeof(FileRecord),
            typeof(FileUploadSession),
            typeof(TenantDataSource),
            typeof(TenantAppDataSourceBinding),
            typeof(AppMember),
            typeof(AppRole),
            typeof(AppUserRole),
            typeof(AppRolePermission),
            typeof(AppPermission),
            typeof(Tenant),
            // Low code module (types already registered above: LowCodeApp, AppEntityAlias, LowCodePage, FormDefinition)
            typeof(LowCodeAppVersion),
            typeof(FormDefinitionVersion),
            // Events / Outbox
            typeof(OutboxMessage),
            // Plugin configuration
            typeof(PluginConfig),
            // Plugin market
            typeof(PluginMarketEntry),
            typeof(PluginMarketVersion),
            // Component templates
            typeof(Atlas.Domain.Templates.ComponentTemplate),
            // Webhooks
            typeof(Atlas.Domain.Integration.WebhookSubscription),
            typeof(Atlas.Domain.Integration.WebhookDeliveryLog),
            // API Connectors
            typeof(Atlas.Domain.Integration.ApiConnector),
            typeof(Atlas.Domain.Integration.ApiConnectorOperation),
            // Integration API Keys
            typeof(Atlas.Domain.Integration.IntegrationApiKey),
            // Message Queue
            typeof(Atlas.Domain.Messaging.QueueMessage),
            // Saga
            typeof(Atlas.Domain.Saga.SagaInstance),
            // Event Subscriptions
            typeof(Atlas.Domain.Events.EventSubscription),
            // License
            typeof(Atlas.Domain.License.LicenseRecord),
            // Productization
            typeof(AppManifest),
            typeof(TenantApplication),
            typeof(AppRelease),
            typeof(RuntimeRoute),
            typeof(PackageArtifact),
            typeof(LicenseGrant),
            typeof(ToolAuthorizationPolicy),
            typeof(AppDesignerSnapshot));
        // 结束 CodeFirst.InitTables 块
        }
        else
        {
            _logger.LogInformation("[DatabaseInitializer] 已跳过 Schema 初始化（DatabaseInitializer:SkipSchemaInit=true）");
        }

        // 兼容历史库：Permission.AppId 误建为 NOT NULL 时，平台权限种子无法插入（AppId 应为 NULL）
        await EnsurePermissionAppIdNullableSchemaAsync(db, cancellationToken);

        await EnsureTenantAppDataSourceBindingBackfillAsync(scope.ServiceProvider, appContextAccessor, db, cancellationToken);

        // 种子数据初始化
        if (_initializerOptions.SkipSeedData)
        {
            // 首次运行自动检测：若 Menu 表为空（新环境/重建 DB），自动忽略 SkipSeedData 标志执行播种
            // 这样无需手动修改配置即可在全新环境中正常启动
            var menuCount = await db.Queryable<Menu>().CountAsync(cancellationToken);
            if (menuCount == 0)
            {
                _logger.LogWarning(
                    "[DatabaseInitializer] 检测到 Menu 表为空（新环境或重建 DB），" +
                    "自动忽略 SkipSeedData=true 配置，执行首次种子数据播种。" +
                    "如需永久跳过播种，请在播种完成后确认 DatabaseInitializer:SkipSeedData=true 并保持数据库不删除。");
            }
            else
            {
                _logger.LogInformation("[DatabaseInitializer] 已跳过种子数据初始化（DatabaseInitializer:SkipSeedData=true）");
                await EnsureBootstrapAdminPlatformFlagAsync(scope, appContextAccessor, cancellationToken);
                // 始终加载 License 状态，不受 SkipSeedData 影响
                await LoadLicenseStatusAsync(scope, cancellationToken);
                return;
            }
        }

        _logger.LogInformation("[DatabaseInitializer] 开始执行种子数据初始化...");

        // 初始化审批模块种子数据（使用 BootstrapAdmin 的 TenantId）
        if (Guid.TryParse(_bootstrapOptions.TenantId, out var seedTenantGuid))
        {
            var approvalSeedService = scope.ServiceProvider.GetRequiredService<ApprovalSeedDataService>();
            var templateSeedDataService = scope.ServiceProvider.GetRequiredService<TemplateSeedDataService>();
            var seedTenantId = new TenantId(seedTenantGuid);
            using var seedScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, seedTenantId));
            await approvalSeedService.InitializeSeedDataAsync(seedTenantId, cancellationToken);
            await templateSeedDataService.InitializeBuiltInTemplatesAsync(seedTenantId, cancellationToken);
        }

        if (Guid.TryParse(_bootstrapOptions.TenantId, out var appTenantGuid))
        {
            var appTenantId = new TenantId(appTenantGuid);
            using var appConfigScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, appTenantId));
            var appConfigRepository = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var appConfigIdGenerator = scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>();
            var appId = appContextAccessor.GetAppId();
            var existingAppConfig = await appConfigRepository.FindByAppIdAsync(appTenantId, appId, cancellationToken);
            if (existingAppConfig is null)
            {
                var appConfig = new AppConfig(appTenantId, appId, appId, appConfigIdGenerator.NextId());
                appConfig.Update(appId, true, false, "默认应用配置", 0);
                await appConfigRepository.AddAsync(appConfig, cancellationToken);
            }
        }

        if (Guid.TryParse(_bootstrapOptions.TenantId, out var configTenantGuid))
        {
            var configTenantId = new TenantId(configTenantGuid);
            using var configScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, configTenantId));
            await EnsureBuiltInSystemConfigsAsync(
                scope.ServiceProvider.GetRequiredService<SystemConfigRepository>(),
                scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>(),
                configTenantId,
                cancellationToken);
        }

        // 启动时加载授权证书状态到内存（不阻塞启动）
        await LoadLicenseStatusAsync(scope, cancellationToken);

        if (!_bootstrapOptions.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_bootstrapOptions.Password))
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning("未配置BootstrapAdmin密码，已跳过创建默认管理员。");
                return;
            }

            throw new InvalidOperationException("生产环境必须配置BootstrapAdmin密码。");
        }

        if (!PasswordPolicy.IsCompliant(_bootstrapOptions.Password, _passwordPolicy, out var message))
        {
            throw new InvalidOperationException($"BootstrapAdmin密码不符合策略：{message}");
        }

        if (!Guid.TryParse(_bootstrapOptions.TenantId, out var tenantGuid))
        {
            throw new InvalidOperationException("BootstrapAdmin TenantId格式错误。");
        }

        var tenantId = new TenantId(tenantGuid);
        using var appContextScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, tenantId));
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var idGeneratorAccessor = scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>();
        var userRoleRepository = scope.ServiceProvider.GetRequiredService<IUserRoleRepository>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var rolePermissionRepository = scope.ServiceProvider.GetRequiredService<IRolePermissionRepository>();
        var roleMenuRepository = scope.ServiceProvider.GetRequiredService<IRoleMenuRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var requiredRoleDefinitions = new (string Code, string Name, string Description, bool IsSystem)[]
        {
            ("SuperAdmin", "超级管理员", "平台超级管理员（全量权限）", true),
            ("Admin", "系统管理员", "系统运维与平台配置管理员", true)
        };
        var optionalRoleDefinitions = new (string Code, string Name, string Description, bool IsSystem)[]
        {
            ("SecurityAdmin", "安全管理员", "安全策略与告警管理员", false),
            ("AuditAdmin", "审计管理员", "审计日志与合规管理员", false),
            ("AssetAdmin", "资产管理员", "资产台账管理员", false),
            ("ApprovalAdmin", "流程管理员", "审批流配置管理员", false)
        };

        var roleSeedMap = requiredRoleDefinitions
            .Concat(optionalRoleDefinitions)
            .ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
        var roleCodes = _bootstrapOptions.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!roleCodes.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase))
        {
            roleCodes.Add("SuperAdmin");
        }
        var roleCodesArray = roleCodes.ToArray();

        var roleCodeSet = roleCodesArray.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allRoleCodes = requiredRoleDefinitions
            .Select(x => x.Code)
            .Concat(roleCodesArray)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existingRoles = await roleRepository.QueryByCodesAsync(tenantId, allRoleCodes, cancellationToken);
        var roleMap = existingRoles.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        var rolesToInsert = new List<Role>();
        foreach (var roleCode in allRoleCodes)
        {
            if (roleMap.ContainsKey(roleCode))
            {
                continue;
            }

            var displayName = roleSeedMap.TryGetValue(roleCode, out var seed) ? seed.Name : roleCode;
            var description = roleSeedMap.TryGetValue(roleCode, out seed) ? seed.Description : roleCode;
            var role = new Role(tenantId, displayName, roleCode, idGeneratorAccessor.NextId());
            role.Update(displayName, description);
            if (roleCodeSet.Contains(roleCode) || (roleSeedMap.TryGetValue(roleCode, out seed) && seed.IsSystem))
            {
                role.MarkSystemRole();
            }
            rolesToInsert.Add(role);
            roleMap[roleCode] = role;
        }

        if (rolesToInsert.Count > 0)
        {
            await roleRepository.AddRangeAsync(rolesToInsert, cancellationToken);
        }

        var roleIds = roleCodesArray
            .Select(roleCode => roleMap[roleCode].Id)
            .Distinct()
            .ToList();

        var permissionSeeds = new (string Code, string Name, string Type)[]
        {
            (PermissionCodes.SystemAdmin, "System Admin", "Api"),
            (PermissionCodes.WorkflowDesign, "Workflow Designer", "Menu"),
            (PermissionCodes.UsersView, "Users View", "Api"),
            (PermissionCodes.UsersCreate, "Users Create", "Api"),
            (PermissionCodes.UsersUpdate, "Users Update", "Api"),
            (PermissionCodes.UsersDelete, "Users Delete", "Api"),
            (PermissionCodes.UsersAssignRoles, "Users Assign Roles", "Api"),
            (PermissionCodes.UsersAssignDepartments, "Users Assign Departments", "Api"),
            (PermissionCodes.UsersAssignPositions, "Users Assign Positions", "Api"),
            (PermissionCodes.RolesView, "Roles View", "Api"),
            (PermissionCodes.RolesCreate, "Roles Create", "Api"),
            (PermissionCodes.RolesUpdate, "Roles Update", "Api"),
            (PermissionCodes.RolesDelete, "Roles Delete", "Api"),
            (PermissionCodes.RolesAssignPermissions, "Roles Assign Permissions", "Api"),
            (PermissionCodes.RolesAssignMenus, "Roles Assign Menus", "Api"),
            (PermissionCodes.PermissionsView, "Permissions View", "Api"),
            (PermissionCodes.PermissionsCreate, "Permissions Create", "Api"),
            (PermissionCodes.PermissionsUpdate, "Permissions Update", "Api"),
            (PermissionCodes.DepartmentsView, "Departments View", "Api"),
            (PermissionCodes.DepartmentsAll, "Departments All", "Api"),
            (PermissionCodes.DepartmentsCreate, "Departments Create", "Api"),
            (PermissionCodes.DepartmentsUpdate, "Departments Update", "Api"),
            (PermissionCodes.DepartmentsDelete, "Departments Delete", "Api"),
            (PermissionCodes.PositionsView, "Positions View", "Api"),
            (PermissionCodes.PositionsCreate, "Positions Create", "Api"),
            (PermissionCodes.PositionsUpdate, "Positions Update", "Api"),
            (PermissionCodes.PositionsDelete, "Positions Delete", "Api"),
            (PermissionCodes.MenusView, "Menus View", "Api"),
            (PermissionCodes.MenusAll, "Menus All", "Api"),
            (PermissionCodes.MenusCreate, "Menus Create", "Api"),
            (PermissionCodes.MenusUpdate, "Menus Update", "Api"),
            (PermissionCodes.MenusDelete, "Menus Delete", "Api"),
            (PermissionCodes.AppsView, "Apps View", "Api"),
            (PermissionCodes.AppsUpdate, "Apps Update", "Api"),
            (PermissionCodes.AppMembersView, "App Members View", "Api"),
            (PermissionCodes.AppMembersUpdate, "App Members Update", "Api"),
            (PermissionCodes.AppRolesView, "App Roles View", "Api"),
            (PermissionCodes.AppRolesUpdate, "App Roles Update", "Api"),
            (PermissionCodes.AppAdmin, "App Admin", "Api"),
            (PermissionCodes.AppUser, "App User", "Api"),
            (PermissionCodes.DebugView, "Debug View", "Api"),
            (PermissionCodes.DebugRun, "Debug Run", "Api"),
            (PermissionCodes.DebugManage, "Debug Manage", "Api"),
            (PermissionCodes.ProjectsView, "Projects View", "Api"),
            (PermissionCodes.ProjectsCreate, "Projects Create", "Api"),
            (PermissionCodes.ProjectsUpdate, "Projects Update", "Api"),
            (PermissionCodes.ProjectsDelete, "Projects Delete", "Api"),
            (PermissionCodes.ProjectsAssignUsers, "Projects Assign Users", "Api"),
            (PermissionCodes.ProjectsAssignDepartments, "Projects Assign Departments", "Api"),
            (PermissionCodes.ProjectsAssignPositions, "Projects Assign Positions", "Api"),
            (PermissionCodes.AuditView, "Audit View", "Api"),
            (PermissionCodes.AssetsView, "Assets View", "Api"),
            (PermissionCodes.AssetsCreate, "Assets Create", "Api"),
            (PermissionCodes.AlertView, "Alert View", "Api"),
            (PermissionCodes.ModelConfigView, "Model Config View", "Api"),
            (PermissionCodes.ModelConfigCreate, "Model Config Create", "Api"),
            (PermissionCodes.ModelConfigUpdate, "Model Config Update", "Api"),
            (PermissionCodes.ModelConfigDelete, "Model Config Delete", "Api"),
            (PermissionCodes.AgentView, "Agent View", "Api"),
            (PermissionCodes.AgentCreate, "Agent Create", "Api"),
            (PermissionCodes.AgentUpdate, "Agent Update", "Api"),
            (PermissionCodes.AgentDelete, "Agent Delete", "Api"),
            (PermissionCodes.ConversationView, "Conversation View", "Api"),
            (PermissionCodes.ConversationCreate, "Conversation Create", "Api"),
            (PermissionCodes.ConversationDelete, "Conversation Delete", "Api"),
            (PermissionCodes.KnowledgeBaseView, "Knowledge Base View", "Api"),
            (PermissionCodes.KnowledgeBaseCreate, "Knowledge Base Create", "Api"),
            (PermissionCodes.KnowledgeBaseUpdate, "Knowledge Base Update", "Api"),
            (PermissionCodes.KnowledgeBaseDelete, "Knowledge Base Delete", "Api"),
            (PermissionCodes.AiWorkflowView, "AI Workflow View", "Api"),
            (PermissionCodes.AiWorkflowCreate, "AI Workflow Create", "Api"),
            (PermissionCodes.AiWorkflowUpdate, "AI Workflow Update", "Api"),
            (PermissionCodes.AiWorkflowDelete, "AI Workflow Delete", "Api"),
            (PermissionCodes.AiWorkflowExecute, "AI Workflow Execute", "Api"),
            (PermissionCodes.AiWorkflowDebug, "AI Workflow Debug", "Api"),
            (PermissionCodes.AiDatabaseView, "AI Database View", "Api"),
            (PermissionCodes.AiDatabaseCreate, "AI Database Create", "Api"),
            (PermissionCodes.AiDatabaseUpdate, "AI Database Update", "Api"),
            (PermissionCodes.AiDatabaseDelete, "AI Database Delete", "Api"),
            (PermissionCodes.AiVariableView, "AI Variable View", "Api"),
            (PermissionCodes.AiVariableCreate, "AI Variable Create", "Api"),
            (PermissionCodes.AiVariableUpdate, "AI Variable Update", "Api"),
            (PermissionCodes.AiVariableDelete, "AI Variable Delete", "Api"),
            (PermissionCodes.AiPluginView, "AI Plugin View", "Api"),
            (PermissionCodes.AiPluginCreate, "AI Plugin Create", "Api"),
            (PermissionCodes.AiPluginUpdate, "AI Plugin Update", "Api"),
            (PermissionCodes.AiPluginDelete, "AI Plugin Delete", "Api"),
            (PermissionCodes.AiPluginPublish, "AI Plugin Publish", "Api"),
            (PermissionCodes.AiPluginDebug, "AI Plugin Debug", "Api"),
            (PermissionCodes.AiAppView, "AI App View", "Api"),
            (PermissionCodes.AiAppCreate, "AI App Create", "Api"),
            (PermissionCodes.AiAppUpdate, "AI App Update", "Api"),
            (PermissionCodes.AiAppDelete, "AI App Delete", "Api"),
            (PermissionCodes.AiAppPublish, "AI App Publish", "Api"),
            (PermissionCodes.AiPromptView, "AI Prompt View", "Api"),
            (PermissionCodes.AiPromptCreate, "AI Prompt Create", "Api"),
            (PermissionCodes.AiPromptUpdate, "AI Prompt Update", "Api"),
            (PermissionCodes.AiPromptDelete, "AI Prompt Delete", "Api"),
            (PermissionCodes.AiMarketplaceView, "AI Marketplace View", "Api"),
            (PermissionCodes.AiMarketplaceCreate, "AI Marketplace Create", "Api"),
            (PermissionCodes.AiMarketplaceUpdate, "AI Marketplace Update", "Api"),
            (PermissionCodes.AiMarketplaceDelete, "AI Marketplace Delete", "Api"),
            (PermissionCodes.AiMarketplacePublish, "AI Marketplace Publish", "Api"),
            (PermissionCodes.AiSearchView, "AI Search View", "Api"),
            (PermissionCodes.AiSearchUpdate, "AI Search Update", "Api"),
            (PermissionCodes.AiAdminConfigView, "AI Admin Config View", "Api"),
            (PermissionCodes.AiAdminConfigUpdate, "AI Admin Config Update", "Api"),
            (PermissionCodes.AiWorkspaceView, "AI Workspace View", "Api"),
            (PermissionCodes.AiWorkspaceUpdate, "AI Workspace Update", "Api"),
            (PermissionCodes.AiDevopsView, "AI DevOps View", "Api"),
            (PermissionCodes.AiShortcutView, "AI Shortcut View", "Api"),
            (PermissionCodes.AiShortcutManage, "AI Shortcut Manage", "Api"),
            (PermissionCodes.PersonalAccessTokenView, "PAT View", "Api"),
            (PermissionCodes.PersonalAccessTokenCreate, "PAT Create", "Api"),
            (PermissionCodes.PersonalAccessTokenUpdate, "PAT Update", "Api"),
            (PermissionCodes.PersonalAccessTokenDelete, "PAT Delete", "Api"),
            (PermissionCodes.ApprovalFlowView, "Approval Flow View", "Api"),
            (PermissionCodes.ApprovalFlowManage, "Approval Flow Manage", "Api"),
            (PermissionCodes.ApprovalFlowCreate, "Approval Flow Create", "Api"),
            (PermissionCodes.ApprovalFlowUpdate, "Approval Flow Update", "Api"),
            (PermissionCodes.ApprovalFlowPublish, "Approval Flow Publish", "Api"),
            (PermissionCodes.ApprovalFlowDelete, "Approval Flow Delete", "Api"),
            (PermissionCodes.ApprovalFlowDisable, "Approval Flow Disable", "Api"),
            (PermissionCodes.VisualizationView, "Visualization View", "Api"),
            (PermissionCodes.VisualizationProcessSave, "Visualization Process Save", "Api"),
            (PermissionCodes.VisualizationProcessUpdate, "Visualization Process Update", "Api"),
            (PermissionCodes.VisualizationProcessPublish, "Visualization Process Publish", "Api"),
            (PermissionCodes.DictTypeView, "Dict Type View", "Api"),
            (PermissionCodes.DictTypeCreate, "Dict Type Create", "Api"),
            (PermissionCodes.DictTypeUpdate, "Dict Type Update", "Api"),
            (PermissionCodes.DictTypeDelete, "Dict Type Delete", "Api"),
            (PermissionCodes.DictDataView, "Dict Data View", "Api"),
            (PermissionCodes.DictDataCreate, "Dict Data Create", "Api"),
            (PermissionCodes.DictDataUpdate, "Dict Data Update", "Api"),
            (PermissionCodes.DictDataDelete, "Dict Data Delete", "Api"),
            (PermissionCodes.ConfigView, "Config View", "Api"),
            (PermissionCodes.ConfigCreate, "Config Create", "Api"),
            (PermissionCodes.ConfigUpdate, "Config Update", "Api"),
            (PermissionCodes.ConfigDelete, "Config Delete", "Api"),
            (PermissionCodes.LoginLogView, "Login Log View", "Api"),
            (PermissionCodes.LoginLogDelete, "Login Log Delete", "Api"),
            (PermissionCodes.OnlineUsersView, "Online Users View", "Api"),
            (PermissionCodes.OnlineUsersForceLogout, "Online Users Force Logout", "Api"),
            (PermissionCodes.MonitorView, "Monitor View", "Api"),
            (PermissionCodes.NotificationView, "Notification View", "Api"),
            (PermissionCodes.NotificationCreate, "Notification Create", "Api"),
            (PermissionCodes.NotificationUpdate, "Notification Update", "Api"),
            (PermissionCodes.NotificationDelete, "Notification Delete", "Api"),
            (PermissionCodes.JobView, "Job View", "Api"),
            (PermissionCodes.JobCreate, "Job Create", "Api"),
            (PermissionCodes.JobUpdate, "Job Update", "Api"),
            (PermissionCodes.JobDelete, "Job Delete", "Api"),
            (PermissionCodes.JobTrigger, "Job Trigger", "Api"),
            (PermissionCodes.DataScopeManage, "Data Scope Manage", "Api"),
            (PermissionCodes.FileUpload, "File Upload", "Api"),
            (PermissionCodes.FileDownload, "File Download", "Api"),
            (PermissionCodes.FileDelete, "File Delete", "Api")
        };

        var permissionIdMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        Permission? adminPermission = null;
        Permission? workflowPermission = null;

        var permissionCodes = permissionSeeds
            .Select(x => x.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingPermissions = await db.Queryable<Permission>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(permissionCodes, x.Code))
            .ToListAsync(cancellationToken);
        var permissionMap = existingPermissions.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        var permissionsToInsert = new List<Permission>();
        foreach (var seed in permissionSeeds)
        {
            if (permissionMap.ContainsKey(seed.Code))
            {
                continue;
            }

            var permission = new Permission(tenantId, seed.Name, seed.Code, seed.Type, idGeneratorAccessor.NextId());
            permissionsToInsert.Add(permission);
            permissionMap[seed.Code] = permission;
        }

        if (permissionsToInsert.Count > 0)
        {
            await db.Insertable(permissionsToInsert).ExecuteCommandAsync(cancellationToken);
        }

        foreach (var permission in permissionMap.Values)
        {
            permissionIdMap[permission.Code] = permission.Id;
            if (string.Equals(permission.Code, PermissionCodes.SystemAdmin, StringComparison.OrdinalIgnoreCase))
            {
                adminPermission = permission;
            }

            if (string.Equals(permission.Code, PermissionCodes.WorkflowDesign, StringComparison.OrdinalIgnoreCase))
            {
                workflowPermission = permission;
            }
        }

        if (adminPermission is null || workflowPermission is null)
        {
            throw new InvalidOperationException("初始化权限失败：缺少 system:admin 或 workflow:design。");
        }

        var menuSeeds = new (
            string Name,
            string Path,
            string? ParentPath,
            int SortOrder,
            string MenuType,
            string? Component,
            string? Icon,
            string? Perms,
            string? Query,
            bool IsFrame,
            bool IsCache,
            string Visible,
            string Status,
            string? PermissionCode,
            bool IsHidden)[]
        {
            ("首页", "/", null, 0, "C", "home", "dashboard", PermissionCodes.SystemAdmin, null, false, false, "0", "0", PermissionCodes.SystemAdmin, false),

            ("平台控制台", "/console", null, 5, "M", "Layout", "dashboard", null, null, false, false, "0", "0", null, false),
            ("控制台首页", "/console/apps", "/console", 6, "C", "console/ConsolePage", "appstore", PermissionCodes.AppsView, null, false, true, "0", "0", PermissionCodes.AppsView, false),
            ("控制台数据源", "/console/datasources", "/console", 7, "C", "system/TenantDataSourcesPage", "database", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("控制台系统设置", "/console/settings/system/configs", "/console", 8, "C", "system/SystemConfigsPage", "tool", PermissionCodes.ConfigView, null, false, true, "0", "0", PermissionCodes.ConfigView, false),

            ("安全中心", "/security", null, 10, "M", "Layout", "safety-certificate", null, null, false, false, "0", "0", null, false),
            ("资产管理", "/assets", "/security", 11, "C", "AssetsPage", "database", PermissionCodes.AssetsView, null, false, true, "0", "0", PermissionCodes.AssetsView, false),
            ("审计日志", "/audit", "/security", 12, "C", "AuditPage", "file-search", PermissionCodes.AuditView, null, false, true, "0", "0", PermissionCodes.AuditView, false),
            ("告警管理", "/alert", "/security", 13, "C", "AlertPage", "bell", PermissionCodes.AlertView, null, false, true, "0", "0", PermissionCodes.AlertView, false),
            ("AI 平台", "/ai", null, 14, "M", "Layout", "robot", null, null, false, false, "0", "0", null, false),
            ("模型配置", "/settings/ai/model-configs", "/ai", 15, "C", "ai/ModelConfigsPage", "api", PermissionCodes.ModelConfigView, null, false, true, "0", "0", PermissionCodes.ModelConfigView, false),
            ("Agent 管理", "/ai/agents", "/ai", 16, "C", "ai/AgentListPage", "experiment", PermissionCodes.AgentView, null, false, true, "0", "0", PermissionCodes.AgentView, false),
            ("Agent 编辑", "/ai/agents/:id/edit", "/ai", 17, "C", "ai/AgentEditorPage", "edit", PermissionCodes.AgentView, null, false, true, "0", "0", PermissionCodes.AgentView, true),
            ("知识库管理", "/ai/knowledge-bases", "/ai", 18, "C", "ai/KnowledgeBaseListPage", "book", PermissionCodes.KnowledgeBaseView, null, false, true, "0", "0", PermissionCodes.KnowledgeBaseView, false),
            ("知识库详情", "/ai/knowledge-bases/:id", "/ai", 19, "C", "ai/KnowledgeBaseDetailPage", "read", PermissionCodes.KnowledgeBaseView, null, false, true, "0", "0", PermissionCodes.KnowledgeBaseView, true),
            ("记忆管理", "/ai/memories", "/ai", 20, "C", "ai/UserMemorySettingsPage", "history", PermissionCodes.AgentView, null, false, true, "0", "0", PermissionCodes.AgentView, false),
            ("数据库管理", "/ai/databases", "/ai", 20, "C", "ai/AiDatabaseListPage", "database", PermissionCodes.AiDatabaseView, null, false, true, "0", "0", PermissionCodes.AiDatabaseView, false),
            ("数据库详情", "/ai/databases/:id", "/ai", 21, "C", "ai/AiDatabaseDetailPage", "table", PermissionCodes.AiDatabaseView, null, false, true, "0", "0", PermissionCodes.AiDatabaseView, true),
            ("变量管理", "/ai/variables", "/ai", 22, "C", "ai/AiVariablesPage", "code", PermissionCodes.AiVariableView, null, false, true, "0", "0", PermissionCodes.AiVariableView, false),
            ("插件管理", "/ai/plugins", "/ai", 23, "C", "ai/AiPluginListPage", "api", PermissionCodes.AiPluginView, null, false, true, "0", "0", PermissionCodes.AiPluginView, false),
            ("插件详情", "/ai/plugins/:id", "/ai", 24, "C", "ai/AiPluginDetailPage", "setting", PermissionCodes.AiPluginView, null, false, true, "0", "0", PermissionCodes.AiPluginView, true),
            ("应用管理", "/ai/apps", "/ai", 25, "C", "ai/AiAppListPage", "appstore", PermissionCodes.AiAppView, null, false, true, "0", "0", PermissionCodes.AiAppView, false),
            ("应用编辑", "/ai/apps/:id/edit", "/ai", 26, "C", "ai/AiAppEditorPage", "edit", PermissionCodes.AiAppView, null, false, true, "0", "0", PermissionCodes.AiAppView, true),
            ("Prompt 模板", "/ai/prompts", "/ai", 27, "C", "ai/AiPromptLibraryPage", "snippets", PermissionCodes.AiPromptView, null, false, true, "0", "0", PermissionCodes.AiPromptView, false),
            ("开放平台", "/ai/open-platform", "/ai", 28, "C", "ai/AiOpenPlatformPage", "cloud", PermissionCodes.PersonalAccessTokenView, null, false, true, "0", "0", PermissionCodes.PersonalAccessTokenView, false),
            ("AI 市场", "/ai/marketplace", "/ai", 29, "C", "ai/AiMarketplacePage", "shop", PermissionCodes.AiMarketplaceView, null, false, true, "0", "0", PermissionCodes.AiMarketplaceView, false),
            ("AI 市场详情", "/ai/marketplace/:id", "/ai", 30, "C", "ai/AiMarketplaceDetailPage", "profile", PermissionCodes.AiMarketplaceView, null, false, true, "0", "0", PermissionCodes.AiMarketplaceView, true),
            ("统一搜索", "/ai/search", "/ai", 31, "C", "ai/AiSearchResultsPage", "search", PermissionCodes.AiSearchView, null, false, true, "0", "0", PermissionCodes.AiSearchView, false),
            ("AI 工作台", "/ai/workspace", "/ai", 32, "C", "ai/AiWorkspacePage", "desktop", PermissionCodes.AiWorkspaceView, null, false, true, "0", "0", PermissionCodes.AiWorkspaceView, false),
            ("AI 资源库", "/ai/library", "/ai", 33, "C", "ai/AiLibraryPage", "inbox", PermissionCodes.AiWorkspaceView, null, false, true, "0", "0", PermissionCodes.AiWorkspaceView, false),
            ("AI 测试集", "/ai/devops/test-sets", "/ai", 34, "C", "ai/AiTestSetsPage", "experiment", PermissionCodes.AiDevopsView, null, false, true, "0", "0", PermissionCodes.AiDevopsView, false),
            ("AI Mock 集", "/ai/devops/mock-sets", "/ai", 35, "C", "ai/AiMockSetsPage", "api", PermissionCodes.AiDevopsView, null, false, true, "0", "0", PermissionCodes.AiDevopsView, false),
            ("快捷命令", "/ai/shortcuts", "/ai", 36, "C", "ai/AiShortcutsPage", "thunderbolt", PermissionCodes.AiShortcutView, null, false, true, "0", "0", PermissionCodes.AiShortcutView, false),

            ("低代码中心", "/lowcode", null, 20, "M", "Layout", "appstore", null, null, false, false, "0", "0", null, false),
            ("应用管理", "/lowcode/apps", "/lowcode", 21, "C", "lowcode/AppListPage", "appstore-add", PermissionCodes.AppsView, null, false, true, "0", "0", PermissionCodes.AppsView, false),
            ("表单管理", "/lowcode/forms", "/lowcode", 22, "C", "lowcode/FormListPage", "form", PermissionCodes.AppsView, null, false, true, "0", "0", PermissionCodes.AppsView, false),
            ("插件市场", "/lowcode/plugin-market", "/lowcode", 23, "C", "lowcode/PluginMarketPage", "shopping", PermissionCodes.AppsView, null, false, true, "0", "0", PermissionCodes.AppsView, false),
            ("回写监控", "/monitor/writeback-failures", "/lowcode", 24, "C", "lowcode/WritebackMonitorPage", "warning", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),

            ("流程中心", "/process", null, 25, "M", "Layout", "cluster", null, null, false, false, "0", "0", null, false),
            ("流程定义", "/approval/flows", "/process", 26, "C", "ApprovalFlowsPage", "apartment", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("发起审批", "/process/start", "/process", 27, "C", "ApprovalStartPage", "send", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我的待办", "/process/inbox", "/process", 28, "C", "ApprovalInboxPage", "schedule", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我的已办", "/process/done", "/process", 29, "C", "ApprovalDonePage", "check-circle", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我发起的", "/process/my-requests", "/process", 30, "C", "ApprovalMyRequestsPage", "profile", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我的抄送", "/process/cc", "/process", 31, "C", "ApprovalCcPage", "mail", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("审批设计器", "/process/designer", "/process", 32, "C", "ApprovalDesignerPage", "branches", PermissionCodes.ApprovalFlowUpdate, null, false, true, "0", "0", PermissionCodes.ApprovalFlowUpdate, true),
            ("审批设计详情", "/process/designer/:id", "/process", 33, "C", "ApprovalDesignerPage", "branches", PermissionCodes.ApprovalFlowUpdate, null, false, true, "0", "0", PermissionCodes.ApprovalFlowUpdate, true),
            ("审批任务详情", "/process/tasks/:id", "/process", 34, "C", "ApprovalTaskDetailPage", "file-search", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, true),
            ("审批实例详情", "/process/instances/:id", "/process", 35, "C", "ApprovalInstanceDetailPage", "file-text", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, true),
            ("流程定义管理", "/process/manage/flows", "/process", 36, "C", "ApprovalFlowManagePage", "control", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("流程实例管理", "/process/manage/instances", "/process", 37, "C", "ApprovalInstanceManagePage", "appstore", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("工作流设计", "/workflow/designer", "/process", 38, "C", "WorkflowDesignerPage", "branches", workflowPermission.Code, null, false, true, "0", "0", workflowPermission.Code, false),

            ("运维监控", "/monitor", null, 35, "M", "Layout", "monitor", null, null, false, false, "0", "0", null, false),
            ("消息队列监控", "/monitor/message-queue", "/monitor", 36, "C", "monitor/MessageQueuePage", "deployment-unit", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),

            ("系统管理", "/system", null, 30, "M", "Layout", "setting", null, null, false, false, "0", "0", null, false),
            ("组织架构", "/settings/org/departments", "/system", 31, "C", "system/DepartmentsPage", "cluster", PermissionCodes.DepartmentsView, null, false, true, "0", "0", PermissionCodes.DepartmentsView, false),
            ("租户管理", "/settings/org/tenants", "/system", 31, "C", "system/TenantsPage", "global", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("职位名称", "/settings/org/positions", "/system", 32, "C", "system/PositionsPage", "idcard", PermissionCodes.PositionsView, null, false, true, "0", "0", PermissionCodes.PositionsView, false),
            ("员工管理", "/settings/org/users", "/system", 33, "C", "system/UsersPage", "user", PermissionCodes.UsersView, null, false, true, "0", "0", PermissionCodes.UsersView, false),
            ("角色管理", "/settings/auth/roles", "/system", 34, "C", "system/RolesPage", "team", PermissionCodes.RolesView, null, false, true, "0", "0", PermissionCodes.RolesView, false),
            ("个人访问令牌", "/settings/auth/pats", "/system", 35, "C", "settings/PersonalAccessTokensPage", "safety", PermissionCodes.PersonalAccessTokenView, null, false, true, "0", "0", PermissionCodes.PersonalAccessTokenView, false),
            ("菜单管理", "/settings/auth/menus", "/system", 33, "C", "system/MenusPage", "menu", PermissionCodes.MenusView, null, false, true, "0", "0", PermissionCodes.MenusView, false),
            ("项目管理", "/settings/projects", "/system", 34, "C", "system/ProjectsPage", "project", PermissionCodes.ProjectsView, null, false, true, "0", "0", PermissionCodes.ProjectsView, false),
            ("数据源管理", "/settings/system/datasources", "/system", 35, "C", "system/TenantDataSourcesPage", "database", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("字典管理", "/settings/system/dict-types", "/system", 36, "C", "system/DictTypesPage", "book", PermissionCodes.DictTypeView, null, false, true, "0", "0", PermissionCodes.DictTypeView, false),
            ("参数配置", "/settings/system/configs", "/system", 37, "C", "system/SystemConfigsPage", "tool", PermissionCodes.ConfigView, null, false, true, "0", "0", PermissionCodes.ConfigView, false),
            ("AI 管理配置", "/admin/ai-config", "/system", 38, "C", "admin/AiConfigPage", "control", PermissionCodes.AiAdminConfigView, null, false, true, "0", "0", PermissionCodes.AiAdminConfigView, false),
            ("通知中心", "/system/notifications", "/system", 39, "C", "system/NotificationsPage", "notification", PermissionCodes.NotificationView, null, false, true, "0", "0", PermissionCodes.NotificationView, false),
            ("插件管理", "/settings/system/plugins", "/system", 40, "C", "system/PluginManagePage", "api", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("Webhook管理", "/settings/system/webhooks", "/system", 41, "C", "system/WebhooksPage", "link", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),

            ("用户查询", "/settings/org/users:query", "/settings/org/users", 1, "F", null, null, PermissionCodes.UsersView, null, false, false, "0", "0", PermissionCodes.UsersView, true),
            ("用户新增", "/settings/org/users:create", "/settings/org/users", 2, "F", null, null, PermissionCodes.UsersCreate, null, false, false, "0", "0", PermissionCodes.UsersCreate, true),
            ("用户修改", "/settings/org/users:update", "/settings/org/users", 3, "F", null, null, PermissionCodes.UsersUpdate, null, false, false, "0", "0", PermissionCodes.UsersUpdate, true),
            ("用户删除", "/settings/org/users:delete", "/settings/org/users", 4, "F", null, null, PermissionCodes.UsersDelete, null, false, false, "0", "0", PermissionCodes.UsersDelete, true),
            ("角色查询", "/settings/auth/roles:query", "/settings/auth/roles", 1, "F", null, null, PermissionCodes.RolesView, null, false, false, "0", "0", PermissionCodes.RolesView, true),
            ("角色新增", "/settings/auth/roles:create", "/settings/auth/roles", 2, "F", null, null, PermissionCodes.RolesCreate, null, false, false, "0", "0", PermissionCodes.RolesCreate, true),
            ("角色修改", "/settings/auth/roles:update", "/settings/auth/roles", 3, "F", null, null, PermissionCodes.RolesUpdate, null, false, false, "0", "0", PermissionCodes.RolesUpdate, true),
            ("角色删除", "/settings/auth/roles:delete", "/settings/auth/roles", 4, "F", null, null, PermissionCodes.RolesDelete, null, false, false, "0", "0", PermissionCodes.RolesDelete, true),
            ("菜单查询", "/settings/auth/menus:query", "/settings/auth/menus", 1, "F", null, null, PermissionCodes.MenusView, null, false, false, "0", "0", PermissionCodes.MenusView, true),
            ("菜单新增", "/settings/auth/menus:create", "/settings/auth/menus", 2, "F", null, null, PermissionCodes.MenusCreate, null, false, false, "0", "0", PermissionCodes.MenusCreate, true),
            ("菜单修改", "/settings/auth/menus:update", "/settings/auth/menus", 3, "F", null, null, PermissionCodes.MenusUpdate, null, false, false, "0", "0", PermissionCodes.MenusUpdate, true),
            ("菜单删除", "/settings/auth/menus:delete", "/settings/auth/menus", 4, "F", null, null, PermissionCodes.MenusDelete, null, false, false, "0", "0", PermissionCodes.MenusDelete, true),
            ("PAT 查询", "/settings/auth/pats:query", "/settings/auth/pats", 1, "F", null, null, PermissionCodes.PersonalAccessTokenView, null, false, false, "0", "0", PermissionCodes.PersonalAccessTokenView, true),
            ("PAT 新增", "/settings/auth/pats:create", "/settings/auth/pats", 2, "F", null, null, PermissionCodes.PersonalAccessTokenCreate, null, false, false, "0", "0", PermissionCodes.PersonalAccessTokenCreate, true),
            ("PAT 修改", "/settings/auth/pats:update", "/settings/auth/pats", 3, "F", null, null, PermissionCodes.PersonalAccessTokenUpdate, null, false, false, "0", "0", PermissionCodes.PersonalAccessTokenUpdate, true),
            ("PAT 删除", "/settings/auth/pats:delete", "/settings/auth/pats", 4, "F", null, null, PermissionCodes.PersonalAccessTokenDelete, null, false, false, "0", "0", PermissionCodes.PersonalAccessTokenDelete, true),
            ("AI 配置查询", "/admin/ai-config:query", "/admin/ai-config", 5, "F", null, null, PermissionCodes.AiAdminConfigView, null, false, false, "0", "0", PermissionCodes.AiAdminConfigView, true),
            ("AI 配置更新", "/admin/ai-config:update", "/admin/ai-config", 6, "F", null, null, PermissionCodes.AiAdminConfigUpdate, null, false, false, "0", "0", PermissionCodes.AiAdminConfigUpdate, true),
            ("模型配置查询", "/settings/ai/model-configs:query", "/settings/ai/model-configs", 1, "F", null, null, PermissionCodes.ModelConfigView, null, false, false, "0", "0", PermissionCodes.ModelConfigView, true),
            ("模型配置新增", "/settings/ai/model-configs:create", "/settings/ai/model-configs", 2, "F", null, null, PermissionCodes.ModelConfigCreate, null, false, false, "0", "0", PermissionCodes.ModelConfigCreate, true),
            ("模型配置修改", "/settings/ai/model-configs:update", "/settings/ai/model-configs", 3, "F", null, null, PermissionCodes.ModelConfigUpdate, null, false, false, "0", "0", PermissionCodes.ModelConfigUpdate, true),
            ("模型配置删除", "/settings/ai/model-configs:delete", "/settings/ai/model-configs", 4, "F", null, null, PermissionCodes.ModelConfigDelete, null, false, false, "0", "0", PermissionCodes.ModelConfigDelete, true),
            ("Agent 查询", "/ai/agents:query", "/ai/agents", 1, "F", null, null, PermissionCodes.AgentView, null, false, false, "0", "0", PermissionCodes.AgentView, true),
            ("Agent 新增", "/ai/agents:create", "/ai/agents", 2, "F", null, null, PermissionCodes.AgentCreate, null, false, false, "0", "0", PermissionCodes.AgentCreate, true),
            ("Agent 修改", "/ai/agents:update", "/ai/agents", 3, "F", null, null, PermissionCodes.AgentUpdate, null, false, false, "0", "0", PermissionCodes.AgentUpdate, true),
            ("Agent 删除", "/ai/agents:delete", "/ai/agents", 4, "F", null, null, PermissionCodes.AgentDelete, null, false, false, "0", "0", PermissionCodes.AgentDelete, true),
            ("会话查询", "/ai/conversations:query", "/ai", 5, "F", null, null, PermissionCodes.ConversationView, null, false, false, "0", "0", PermissionCodes.ConversationView, true),
            ("会话新增", "/ai/conversations:create", "/ai", 6, "F", null, null, PermissionCodes.ConversationCreate, null, false, false, "0", "0", PermissionCodes.ConversationCreate, true),
            ("会话删除", "/ai/conversations:delete", "/ai", 7, "F", null, null, PermissionCodes.ConversationDelete, null, false, false, "0", "0", PermissionCodes.ConversationDelete, true),
            ("知识库查询", "/ai/knowledge-bases:query", "/ai/knowledge-bases", 8, "F", null, null, PermissionCodes.KnowledgeBaseView, null, false, false, "0", "0", PermissionCodes.KnowledgeBaseView, true),
            ("知识库新增", "/ai/knowledge-bases:create", "/ai/knowledge-bases", 9, "F", null, null, PermissionCodes.KnowledgeBaseCreate, null, false, false, "0", "0", PermissionCodes.KnowledgeBaseCreate, true),
            ("知识库修改", "/ai/knowledge-bases:update", "/ai/knowledge-bases", 10, "F", null, null, PermissionCodes.KnowledgeBaseUpdate, null, false, false, "0", "0", PermissionCodes.KnowledgeBaseUpdate, true),
            ("知识库删除", "/ai/knowledge-bases:delete", "/ai/knowledge-bases", 11, "F", null, null, PermissionCodes.KnowledgeBaseDelete, null, false, false, "0", "0", PermissionCodes.KnowledgeBaseDelete, true),
            ("数据库查询", "/ai/databases:query", "/ai/databases", 12, "F", null, null, PermissionCodes.AiDatabaseView, null, false, false, "0", "0", PermissionCodes.AiDatabaseView, true),
            ("数据库新增", "/ai/databases:create", "/ai/databases", 13, "F", null, null, PermissionCodes.AiDatabaseCreate, null, false, false, "0", "0", PermissionCodes.AiDatabaseCreate, true),
            ("数据库修改", "/ai/databases:update", "/ai/databases", 14, "F", null, null, PermissionCodes.AiDatabaseUpdate, null, false, false, "0", "0", PermissionCodes.AiDatabaseUpdate, true),
            ("数据库删除", "/ai/databases:delete", "/ai/databases", 15, "F", null, null, PermissionCodes.AiDatabaseDelete, null, false, false, "0", "0", PermissionCodes.AiDatabaseDelete, true),
            ("变量查询", "/ai/variables:query", "/ai/variables", 16, "F", null, null, PermissionCodes.AiVariableView, null, false, false, "0", "0", PermissionCodes.AiVariableView, true),
            ("变量新增", "/ai/variables:create", "/ai/variables", 17, "F", null, null, PermissionCodes.AiVariableCreate, null, false, false, "0", "0", PermissionCodes.AiVariableCreate, true),
            ("变量修改", "/ai/variables:update", "/ai/variables", 18, "F", null, null, PermissionCodes.AiVariableUpdate, null, false, false, "0", "0", PermissionCodes.AiVariableUpdate, true),
            ("变量删除", "/ai/variables:delete", "/ai/variables", 19, "F", null, null, PermissionCodes.AiVariableDelete, null, false, false, "0", "0", PermissionCodes.AiVariableDelete, true),
            ("插件查询", "/ai/plugins:query", "/ai/plugins", 20, "F", null, null, PermissionCodes.AiPluginView, null, false, false, "0", "0", PermissionCodes.AiPluginView, true),
            ("插件新增", "/ai/plugins:create", "/ai/plugins", 21, "F", null, null, PermissionCodes.AiPluginCreate, null, false, false, "0", "0", PermissionCodes.AiPluginCreate, true),
            ("插件修改", "/ai/plugins:update", "/ai/plugins", 22, "F", null, null, PermissionCodes.AiPluginUpdate, null, false, false, "0", "0", PermissionCodes.AiPluginUpdate, true),
            ("插件删除", "/ai/plugins:delete", "/ai/plugins", 23, "F", null, null, PermissionCodes.AiPluginDelete, null, false, false, "0", "0", PermissionCodes.AiPluginDelete, true),
            ("插件发布", "/ai/plugins:publish", "/ai/plugins", 24, "F", null, null, PermissionCodes.AiPluginPublish, null, false, false, "0", "0", PermissionCodes.AiPluginPublish, true),
            ("插件调试", "/ai/plugins:debug", "/ai/plugins", 25, "F", null, null, PermissionCodes.AiPluginDebug, null, false, false, "0", "0", PermissionCodes.AiPluginDebug, true),
            ("应用查询", "/ai/apps:query", "/ai/apps", 26, "F", null, null, PermissionCodes.AiAppView, null, false, false, "0", "0", PermissionCodes.AiAppView, true),
            ("应用新增", "/ai/apps:create", "/ai/apps", 27, "F", null, null, PermissionCodes.AiAppCreate, null, false, false, "0", "0", PermissionCodes.AiAppCreate, true),
            ("应用修改", "/ai/apps:update", "/ai/apps", 28, "F", null, null, PermissionCodes.AiAppUpdate, null, false, false, "0", "0", PermissionCodes.AiAppUpdate, true),
            ("应用删除", "/ai/apps:delete", "/ai/apps", 29, "F", null, null, PermissionCodes.AiAppDelete, null, false, false, "0", "0", PermissionCodes.AiAppDelete, true),
            ("应用发布", "/ai/apps:publish", "/ai/apps", 30, "F", null, null, PermissionCodes.AiAppPublish, null, false, false, "0", "0", PermissionCodes.AiAppPublish, true),
            ("Prompt 查询", "/ai/prompts:query", "/ai/prompts", 31, "F", null, null, PermissionCodes.AiPromptView, null, false, false, "0", "0", PermissionCodes.AiPromptView, true),
            ("Prompt 新增", "/ai/prompts:create", "/ai/prompts", 32, "F", null, null, PermissionCodes.AiPromptCreate, null, false, false, "0", "0", PermissionCodes.AiPromptCreate, true),
            ("Prompt 修改", "/ai/prompts:update", "/ai/prompts", 33, "F", null, null, PermissionCodes.AiPromptUpdate, null, false, false, "0", "0", PermissionCodes.AiPromptUpdate, true),
            ("Prompt 删除", "/ai/prompts:delete", "/ai/prompts", 34, "F", null, null, PermissionCodes.AiPromptDelete, null, false, false, "0", "0", PermissionCodes.AiPromptDelete, true),
            ("市场查询", "/ai/marketplace:query", "/ai/marketplace", 35, "F", null, null, PermissionCodes.AiMarketplaceView, null, false, false, "0", "0", PermissionCodes.AiMarketplaceView, true),
            ("市场新增", "/ai/marketplace:create", "/ai/marketplace", 36, "F", null, null, PermissionCodes.AiMarketplaceCreate, null, false, false, "0", "0", PermissionCodes.AiMarketplaceCreate, true),
            ("市场修改", "/ai/marketplace:update", "/ai/marketplace", 37, "F", null, null, PermissionCodes.AiMarketplaceUpdate, null, false, false, "0", "0", PermissionCodes.AiMarketplaceUpdate, true),
            ("市场删除", "/ai/marketplace:delete", "/ai/marketplace", 38, "F", null, null, PermissionCodes.AiMarketplaceDelete, null, false, false, "0", "0", PermissionCodes.AiMarketplaceDelete, true),
            ("市场发布", "/ai/marketplace:publish", "/ai/marketplace", 39, "F", null, null, PermissionCodes.AiMarketplacePublish, null, false, false, "0", "0", PermissionCodes.AiMarketplacePublish, true),
            ("搜索查询", "/ai/search:query", "/ai/search", 40, "F", null, null, PermissionCodes.AiSearchView, null, false, false, "0", "0", PermissionCodes.AiSearchView, true),
            ("搜索记录更新", "/ai/search:update", "/ai/search", 41, "F", null, null, PermissionCodes.AiSearchUpdate, null, false, false, "0", "0", PermissionCodes.AiSearchUpdate, true),
            ("工作台查询", "/ai/workspace:query", "/ai/workspace", 42, "F", null, null, PermissionCodes.AiWorkspaceView, null, false, false, "0", "0", PermissionCodes.AiWorkspaceView, true),
            ("工作台更新", "/ai/workspace:update", "/ai/workspace", 43, "F", null, null, PermissionCodes.AiWorkspaceUpdate, null, false, false, "0", "0", PermissionCodes.AiWorkspaceUpdate, true),
            ("DevOps 查看", "/ai/devops:query", "/ai/devops/test-sets", 44, "F", null, null, PermissionCodes.AiDevopsView, null, false, false, "0", "0", PermissionCodes.AiDevopsView, true),
            ("快捷命令查看", "/ai/shortcuts:query", "/ai/shortcuts", 45, "F", null, null, PermissionCodes.AiShortcutView, null, false, false, "0", "0", PermissionCodes.AiShortcutView, true),
            ("快捷命令管理", "/ai/shortcuts:manage", "/ai/shortcuts", 46, "F", null, null, PermissionCodes.AiShortcutManage, null, false, false, "0", "0", PermissionCodes.AiShortcutManage, true)
        };

        var menuPaths = menuSeeds.Select(x => x.Path).Distinct().ToArray();
        var existingMenus = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(menuPaths, x.Path))
            .ToListAsync(cancellationToken);
        var menuIdMap = existingMenus.ToDictionary(x => x.Path, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var menusToInsert = new List<Menu>();
        foreach (var seed in menuSeeds)
        {
            if (menuIdMap.ContainsKey(seed.Path))
            {
                continue;
            }

            var parentId = 0L;
            if (!string.IsNullOrWhiteSpace(seed.ParentPath) && menuIdMap.TryGetValue(seed.ParentPath, out var resolvedParentId))
            {
                parentId = resolvedParentId;
            }

            var menu = new Menu(
                tenantId,
                seed.Name,
                seed.Path,
                idGeneratorAccessor.NextId(),
                parentId,
                seed.SortOrder,
                seed.MenuType,
                seed.Component,
                seed.Icon,
                seed.Perms,
                seed.Query,
                seed.IsFrame,
                seed.IsCache,
                seed.Visible,
                seed.Status,
                seed.PermissionCode,
                seed.IsHidden);
            menusToInsert.Add(menu);
            menuIdMap[seed.Path] = menu.Id;
        }

        if (menusToInsert.Count > 0)
        {
            await db.Insertable(menusToInsert).ExecuteCommandAsync(cancellationToken);
        }

        var departmentSeeds = new (string Name, string? ParentName, int SortOrder)[]
        {
            ("总部", null, 0),
            ("研发部", "总部", 10),
            ("安全运营部", "总部", 20),
            ("运维部", "总部", 30),
            ("人力资源部", "总部", 40),
            ("财务部", "总部", 50)
        };

        var departmentNameList = departmentSeeds.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existingDepartments = await db.Queryable<Department>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(departmentNameList, x.Name))
            .ToListAsync(cancellationToken);
        var departmentIdMap = existingDepartments.ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var departmentsToInsert = new List<Department>();
        foreach (var seed in departmentSeeds)
        {
            if (departmentIdMap.ContainsKey(seed.Name))
            {
                continue;
            }

            var parentId = 0L;
            if (!string.IsNullOrWhiteSpace(seed.ParentName) &&
                departmentIdMap.TryGetValue(seed.ParentName, out var resolvedParentId))
            {
                parentId = resolvedParentId;
            }

            // Defaulting code to name for seed data
            var department = new Department(tenantId, seed.Name, seed.Name, idGeneratorAccessor.NextId(), parentId, seed.SortOrder);
            departmentsToInsert.Add(department);
            departmentIdMap[seed.Name] = department.Id;
        }

        if (departmentsToInsert.Count > 0)
        {
            await db.Insertable(departmentsToInsert).ExecuteCommandAsync(cancellationToken);
        }

        var positionSeeds = new (string Name, string Code, string Description, bool IsSystem, int SortOrder)[]
        {
            ("安全负责人", "SEC_LEAD", "安全策略与风险管理负责人", true, 10),
            ("安全工程师", "SEC_ENG", "安全工程与检测响应", false, 20),
            ("审计专员", "AUDITOR", "审计与合规执行", false, 30),
            ("资产管理员", "ASSET_ADMIN", "资产台账与生命周期管理", false, 40),
            ("系统管理员", "SYS_ADMIN", "系统配置与运维管理", true, 50),
            ("运维工程师", "OPS_ENG", "平台运维与监控", false, 60)
        };

        var positionCodeList = positionSeeds.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existingPositions = await db.Queryable<Position>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(positionCodeList, x.Code))
            .ToListAsync(cancellationToken);
        var positionCodeMap = existingPositions.ToDictionary(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var positionsToInsert = new List<Position>();
        foreach (var seed in positionSeeds)
        {
            if (positionCodeMap.ContainsKey(seed.Code))
            {
                continue;
            }

            var position = new Position(tenantId, seed.Name, seed.Code, idGeneratorAccessor.NextId());
            position.Update(seed.Name, seed.Description, true, seed.SortOrder);
            if (seed.IsSystem)
            {
                position.MarkSystem();
            }

            positionsToInsert.Add(position);
            positionCodeMap[seed.Code] = position.Id;
        }

        if (positionsToInsert.Count > 0)
        {
            await db.Insertable(positionsToInsert).ExecuteCommandAsync(cancellationToken);
        }

        var permissionIds = permissionIdMap.Values.Distinct().ToArray();
        var existingRolePermissions = await rolePermissionRepository.QueryByRoleIdsAsync(
            tenantId,
            roleIds,
            cancellationToken);
        var existingRolePermissionSet = existingRolePermissions
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        var rolePermissionsToInsert = new List<RolePermission>();
        foreach (var roleId in roleIds)
        {
            foreach (var permissionId in permissionIds)
            {
                if (existingRolePermissionSet.Contains((roleId, permissionId)))
                {
                    continue;
                }

                rolePermissionsToInsert.Add(new RolePermission(tenantId, roleId, permissionId, idGeneratorAccessor.NextId()));
            }
        }

        if (rolePermissionsToInsert.Count > 0)
        {
            await rolePermissionRepository.AddRangeAsync(rolePermissionsToInsert, cancellationToken);
        }

        var requiredMenuPaths = new[]
        {
            "/",
            "/security",
            "/assets",
            "/audit",
            "/alert",
            "/ai",
            "/settings/ai/model-configs",
            "/ai/agents",
            "/ai/knowledge-bases",
            "/ai/memories",
            "/ai/databases",
            "/ai/variables",
            "/ai/plugins",
            "/ai/apps",
            "/ai/prompts",
            "/ai/open-platform",
            "/ai/marketplace",
            "/ai/search",
            "/ai/workspace",
            "/ai/library",
            "/ai/devops/test-sets",
            "/ai/devops/mock-sets",
            "/ai/shortcuts",
            "/lowcode",
            "/lowcode/apps",
            "/lowcode/forms",
            "/lowcode/plugin-market",
            "/monitor/writeback-failures",
            "/process",
            "/approval/flows",
            "/process/start",
            "/process/inbox",
            "/process/done",
            "/process/my-requests",
            "/process/cc",
            "/process/designer",
            "/process/designer/:id",
            "/process/tasks/:id",
            "/process/instances/:id",
            "/process/manage/flows",
            "/process/manage/instances",
            "/workflow/designer",
            "/monitor",
            "/monitor/message-queue",
            "/system",
            "/settings/org/departments",
            "/settings/org/tenants",
            "/settings/org/positions",
            "/settings/org/users",
            "/settings/auth/roles",
            "/settings/auth/pats",
            "/settings/auth/menus",
            "/settings/projects",
            "/settings/system/datasources",
            "/settings/system/dict-types",
            "/settings/system/configs",
            "/admin/ai-config",
            "/system/notifications",
            "/settings/system/plugins",
            "/settings/system/webhooks"
        };
        var menuIds = requiredMenuPaths
            .Select(path => menuIdMap.TryGetValue(path, out var menuId) ? menuId : 0)
            .Where(menuId => menuId > 0)
            .Distinct()
            .ToArray();

        var roleIdArray = roleIds.Distinct().ToArray();
        var existingRoleMenus = await db.Queryable<RoleMenu>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && SqlFunc.ContainsArray(roleIdArray, x.RoleId)
                && SqlFunc.ContainsArray(menuIds, x.MenuId))
            .Select(x => new { x.RoleId, x.MenuId })
            .ToListAsync(cancellationToken);
        var existingRoleMenuSet = existingRoleMenus
            .Select(x => (x.RoleId, x.MenuId))
            .ToHashSet();

        var roleMenusToInsert = new List<RoleMenu>();
        foreach (var roleId in roleIds)
        {
            foreach (var menuId in menuIds)
            {
                if (existingRoleMenuSet.Contains((roleId, menuId)))
                {
                    continue;
                }

                roleMenusToInsert.Add(new RoleMenu(tenantId, roleId, menuId, idGeneratorAccessor.NextId()));
            }
        }

        if (roleMenusToInsert.Count > 0)
        {
            await roleMenuRepository.AddRangeAsync(roleMenusToInsert, cancellationToken);
        }

        var existing = await userRepository.FindByUsernameAsync(tenantId, _bootstrapOptions.Username, cancellationToken);
        if (existing is not null)
        {
            existing.UpdateRoles(string.Join(',', roleCodes));
            if (_bootstrapOptions.IsPlatformAdmin)
            {
                existing.MarkPlatformAdmin();
            }
            else
            {
                existing.UnmarkPlatformAdmin();
            }

            await userRepository.UpdateAsync(existing, cancellationToken);
            await userRoleRepository.DeleteByUserIdAsync(tenantId, existing.Id, cancellationToken);
            await userRoleRepository.AddRangeAsync(
                roleIds.Select(roleId => new UserRole(tenantId, existing.Id, roleId, idGeneratorAccessor.NextId())).ToArray(),
                cancellationToken);
            return;
        }

        var hashed = passwordHasher.HashPassword(_bootstrapOptions.Password);
        var account = new UserAccount(tenantId, _bootstrapOptions.Username, _bootstrapOptions.Username, hashed, idGeneratorAccessor.NextId());
        account.UpdateRoles(string.Join(',', roleCodes));
        account.MarkSystemAccount();
        if (_bootstrapOptions.IsPlatformAdmin)
        {
            account.MarkPlatformAdmin();
        }
        await userRepository.AddAsync(account, cancellationToken);
        await userRoleRepository.AddRangeAsync(
            roleIds.Select(roleId => new UserRole(tenantId, account.Id, roleId, idGeneratorAccessor.NextId())).ToArray(),
            cancellationToken);
        _logger.LogInformation("已创建BootstrapAdmin账号：{Username}", _bootstrapOptions.Username);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureTenantAppDataSourceBindingBackfillAsync(
        IServiceProvider serviceProvider,
        IAppContextAccessor appContextAccessor,
        ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("TenantAppDataSourceBinding", false)
            || !db.DbMaintenance.IsAnyTable("LowCodeApp", false))
        {
            return;
        }

        var legacyBindings = await db.Queryable<LowCodeApp>()
            .Where(item => item.DataSourceId.HasValue)
            .Select(item => new LegacyAppBindingProjection
            {
                TenantIdValue = item.TenantIdValue,
                TenantAppInstanceId = item.Id,
                DataSourceId = item.DataSourceId!.Value,
                CreatedBy = item.CreatedBy,
                UpdatedBy = item.UpdatedBy,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        if (legacyBindings.Count == 0)
        {
            return;
        }

        var appIds = legacyBindings.Select(item => item.TenantAppInstanceId).Distinct().ToArray();
        var tenantIds = legacyBindings.Select(item => item.TenantIdValue).Distinct().ToArray();
        var existedAppKeys = await db.Queryable<TenantAppDataSourceBinding>()
            .Where(item => SqlFunc.ContainsArray(appIds, item.TenantAppInstanceId)
                && SqlFunc.ContainsArray(tenantIds, item.TenantIdValue))
            .Select(item => new { item.TenantIdValue, item.TenantAppInstanceId })
            .ToListAsync(cancellationToken);
        var existedAppSet = existedAppKeys
            .Select(item => (item.TenantIdValue, item.TenantAppInstanceId))
            .ToHashSet();

        var idGenerator = serviceProvider.GetRequiredService<IIdGeneratorAccessor>();
        var backfillRows = new List<TenantAppDataSourceBinding>();
        foreach (var legacy in legacyBindings)
        {
            if (existedAppSet.Contains((legacy.TenantIdValue, legacy.TenantAppInstanceId)))
            {
                continue;
            }

            var now = legacy.UpdatedAt;
            var actor = legacy.UpdatedBy > 0 ? legacy.UpdatedBy : legacy.CreatedBy;
            using var appContextScope = appContextAccessor.BeginScope(
                CreateSystemContext(appContextAccessor, new TenantId(legacy.TenantIdValue)));
            var binding = new TenantAppDataSourceBinding(
                new TenantId(legacy.TenantIdValue),
                legacy.TenantAppInstanceId,
                legacy.DataSourceId,
                TenantAppDataSourceBindingType.Primary,
                actor,
                idGenerator.NextId(),
                now,
                "Migrated from LowCodeApp.DataSourceId");
            backfillRows.Add(binding);
        }

        if (backfillRows.Count == 0)
        {
            return;
        }

        await db.Insertable(backfillRows).ExecuteCommandAsync(cancellationToken);
        _logger.LogInformation(
            "[DatabaseInitializer] 已回填 TenantAppDataSourceBinding 记录 {Count} 条。",
            backfillRows.Count);
    }

    private async Task LoadLicenseStatusAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var licenseService = scope.ServiceProvider.GetService<Atlas.Application.License.Abstractions.ILicenseService>();
            if (licenseService is not null)
            {
                await licenseService.ReloadAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "启动时加载授权证书状态失败，平台将以无授权模式运行");
        }
    }

    private async Task EnsureBootstrapAdminPlatformFlagAsync(
        IServiceScope scope,
        IAppContextAccessor appContextAccessor,
        CancellationToken cancellationToken)
    {
        if (!_bootstrapOptions.Enabled)
        {
            return;
        }

        if (!Guid.TryParse(_bootstrapOptions.TenantId, out var tenantGuid))
        {
            _logger.LogWarning("BootstrapAdmin TenantId 配置无效，跳过平台管理员标记检查。");
            return;
        }

        var tenantId = new TenantId(tenantGuid);
        using var appContextScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, tenantId));
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var account = await userRepository.FindByUsernameAsync(tenantId, _bootstrapOptions.Username, cancellationToken);
        if (account is null)
        {
            return;
        }

        if (_bootstrapOptions.IsPlatformAdmin && !account.IsPlatformAdmin)
        {
            account.MarkPlatformAdmin();
            await userRepository.UpdateAsync(account, cancellationToken);
            _logger.LogInformation("已补齐 BootstrapAdmin 平台管理员标记：{Username}", _bootstrapOptions.Username);
            return;
        }

        if (!_bootstrapOptions.IsPlatformAdmin && account.IsPlatformAdmin)
        {
            account.UnmarkPlatformAdmin();
            await userRepository.UpdateAsync(account, cancellationToken);
            _logger.LogInformation("已移除 BootstrapAdmin 平台管理员标记：{Username}", _bootstrapOptions.Username);
        }
    }

    private static IAppContext CreateSystemContext(IAppContextAccessor appContextAccessor, TenantId tenantId)
    {
        var appId = appContextAccessor.GetAppId();
        var clientContext = new ClientContext(
            ClientType.Backend,
            ClientPlatform.Web,
            ClientChannel.App,
            ClientAgent.Other);
        return new AppContextSnapshot(tenantId, appId, null, clientContext, null);
    }

    private static async Task EnsureUserAccountSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("UserAccount", false))
        {
            return;
        }

        if (!RequiresMissingColumnFix<UserAccount>(db, "IsPlatformAdmin"))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await db.Ado.ExecuteCommandAsync(
            "ALTER TABLE UserAccount ADD COLUMN IsPlatformAdmin INTEGER NOT NULL DEFAULT 0");
    }

    private static Task EnsureAppMembershipSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var missingTables =
            !db.DbMaintenance.IsAnyTable("AppMember", false)
            || !db.DbMaintenance.IsAnyTable("AppRole", false)
            || !db.DbMaintenance.IsAnyTable("AppUserRole", false)
            || !db.DbMaintenance.IsAnyTable("AppRolePermission", false)
            || !db.DbMaintenance.IsAnyTable("AppPermission", false);
        if (!missingTables)
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        db.CodeFirst.InitTables<AppMember, AppRole, AppUserRole, AppRolePermission, AppPermission>();
        return Task.CompletedTask;
    }

    private static async Task EnsureAuthSessionSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AuthSession", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<AuthSession>(db, "RevokedAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<AuthSession>(db, cancellationToken);
    }

    private static async Task EnsurePermissionAppIdNullableSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var tableName = db.EntityMaintenance.GetTableName<Permission>();
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<Permission>(db, "AppId"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<Permission>(db, cancellationToken);
    }

    private static async Task EnsureLowCodeAppSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("LowCodeApp", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<LowCodeApp>(db, "DataSourceId", "PublishedBy", "PublishedAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<LowCodeApp>(db, cancellationToken);
    }

    private static async Task EnsureTenantDataSourceSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("TenantDataSource", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<TenantDataSource>(db, "AppId", "LastTestSuccess", "LastTestedAt", "LastTestMessage", "UpdatedAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<TenantDataSource>(db, cancellationToken);
    }

    private static async Task EnsureProductizationSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        await EnsureAppManifestSchemaAsync(db, cancellationToken);
        await EnsureAppReleaseSchemaAsync(db, cancellationToken);
        await EnsurePackageArtifactSchemaAsync(db, cancellationToken);
        await EnsureLicenseGrantSchemaAsync(db, cancellationToken);
        await EnsureToolAuthorizationPolicySchemaAsync(db, cancellationToken);
    }

    private static async Task EnsureWorkflowExecutionSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("WorkflowExecution", false))
        {
            return;
        }

        if (!RequiresMissingColumnFix<WorkflowExecution>(db, "AppId", "ReleaseId", "RuntimeContextId"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<WorkflowExecution>(db, cancellationToken);
    }

    private static async Task EnsureAiPluginSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AiPlugin", false))
        {
            return;
        }

        await AddColumnIfMissingAsync(db, "AiPlugin", "SourceType", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiPlugin", "AuthType", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiPlugin", "AuthConfigJson", "TEXT NOT NULL DEFAULT '{}'", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiPlugin", "ToolSchemaJson", "TEXT NOT NULL DEFAULT '{}'", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiPlugin", "OpenApiSpecJson", "TEXT NOT NULL DEFAULT '{}'", cancellationToken);
    }

    private static async Task EnsureAiMemorySchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var missingShortTerm = !db.DbMaintenance.IsAnyTable("ShortTermMemory", false);
        var missingLongTerm = !db.DbMaintenance.IsAnyTable("LongTermMemory", false);
        if (missingShortTerm || missingLongTerm)
        {
            cancellationToken.ThrowIfCancellationRequested();
            db.CodeFirst.InitTables<ShortTermMemory, LongTermMemory>();
        }

        if (db.DbMaintenance.IsAnyTable("Agent", false))
        {
            await AddColumnIfMissingAsync(db, "Agent", "EnableMemory", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
            await AddColumnIfMissingAsync(db, "Agent", "EnableShortTermMemory", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
            await AddColumnIfMissingAsync(db, "Agent", "EnableLongTermMemory", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
            await AddColumnIfMissingAsync(db, "Agent", "LongTermMemoryTopK", "INTEGER NOT NULL DEFAULT 3", cancellationToken);
        }
    }

    private static async Task EnsureAppManifestSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AppManifest", false)) return;
        if (!RequiresNullableColumnFix<AppManifest>(db, "DataSourceId", "PublishedBy", "PublishedAt")) return;
        await RebuildTableViaOrmAsync<AppManifest>(db, cancellationToken);
    }

    private static async Task EnsureAppReleaseSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AppRelease", false)) return;
        if (!RequiresNullableColumnFix<AppRelease>(db, "RollbackPointId")) return;
        await RebuildTableViaOrmAsync<AppRelease>(db, cancellationToken);
    }

    private static async Task EnsurePackageArtifactSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("PackageArtifact", false)) return;
        if (!RequiresNullableColumnFix<PackageArtifact>(db, "ExportedBy", "ExportedAt", "ImportedBy", "ImportedAt")) return;
        await RebuildTableViaOrmAsync<PackageArtifact>(db, cancellationToken);
    }

    private static async Task EnsureLicenseGrantSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("LicenseGrant", false)) return;
        if (!RequiresNullableColumnFix<LicenseGrant>(db, "ExpiresAt")) return;
        await RebuildTableViaOrmAsync<LicenseGrant>(db, cancellationToken);
    }

    private static async Task EnsureToolAuthorizationPolicySchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ToolAuthorizationPolicy", false)) return;
        if (!RequiresNullableColumnFix<ToolAuthorizationPolicy>(db, "ApprovalFlowId")) return;
        await RebuildTableViaOrmAsync<ToolAuthorizationPolicy>(db, cancellationToken);
    }

    private static async Task EnsureApprovalSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        await EnsureApprovalFlowDefinitionSchemaAsync(db, cancellationToken);
        await EnsureApprovalProcessInstanceSchemaAsync(db, cancellationToken);
        await EnsureApprovalTaskSchemaAsync(db, cancellationToken);
        await EnsureApprovalHistoryEventSchemaAsync(db, cancellationToken);
        await EnsureApprovalNodeExecutionSchemaAsync(db, cancellationToken);
        await EnsureApprovalCopyRecordSchemaAsync(db, cancellationToken);
    }

    private static async Task EnsureApprovalFlowDefinitionSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalFlowDefinition", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalFlowDefinition>(db, "PublishedAt", "PublishedByUserId", "VisibilityScopeJson", "Category", "Description"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalFlowDefinition>(db, cancellationToken);
    }

    private static async Task EnsureApprovalProcessInstanceSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalProcessInstance", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalProcessInstance>(db, "DataJson", "EndedAt", "CurrentNodeId", "ParentInstanceId", "Priority", "InstanceNo", "CurrentNodeName"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalProcessInstance>(db, cancellationToken);
    }

    private static async Task EnsureApprovalTaskSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalTask", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalTask>(db, "DecisionByUserId", "DecisionAt", "Comment", "OriginalAssigneeValue", "Weight", "ParentTaskId", "DelegatorUserId", "ViewedAt", "TaskType"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalTask>(db, cancellationToken);
    }

    private static async Task EnsureApprovalHistoryEventSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalHistoryEvent", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalHistoryEvent>(db, "FromNode", "ToNode", "PayloadJson", "ActorUserId"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalHistoryEvent>(db, cancellationToken);
    }

    private static async Task EnsureApprovalNodeExecutionSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalNodeExecution", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalNodeExecution>(db, "CompletedAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalNodeExecution>(db, cancellationToken);
    }

    private static async Task EnsureApprovalCopyRecordSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalCopyRecord", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalCopyRecord>(db, "ReadAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalCopyRecord>(db, cancellationToken);
    }

    private static async Task RebuildTableViaOrmAsync<TEntity>(ISqlSugarClient db, CancellationToken cancellationToken)
        where TEntity : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tableName = db.EntityMaintenance.GetTableName<TEntity>();
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var data = await db.Queryable<TEntity>().ToListAsync(cancellationToken);
        db.DbMaintenance.DropTable(tableName);
        db.CodeFirst.InitTables<TEntity>();
        if (data.Count > 0)
        {
            await db.Insertable(data).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static bool RequiresNullableColumnFix<TEntity>(ISqlSugarClient db, params string[] columnNames)
        where TEntity : class, new()
    {
        var tableName = db.EntityMaintenance.GetTableName<TEntity>();
        var columns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false);
        foreach (var columnName in columnNames)
        {
            var column = columns.FirstOrDefault(c => string.Equals(c.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));
            if (column is not null && !column.IsNullable)
            {
                return true;
            }
        }

        return false;
    }

    private static bool RequiresMissingColumnFix<TEntity>(ISqlSugarClient db, params string[] requiredColumnNames)
        where TEntity : class, new()
    {
        var tableName = db.EntityMaintenance.GetTableName<TEntity>();
        var columns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false);
        foreach (var columnName in requiredColumnNames)
        {
            var hasColumn = columns.Any(c => string.Equals(c.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));
            if (!hasColumn)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task AddColumnIfMissingAsync(
        ISqlSugarClient db,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        var columns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false);
        var hasColumn = columns.Any(c => string.Equals(c.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));
        if (hasColumn)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await db.Ado.ExecuteCommandAsync(
            $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
    }

    private static async Task EnsureRefreshTokenSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("RefreshToken", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<RefreshToken>(db, "RevokedAt", "ReplacedById"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<RefreshToken>(db, cancellationToken);
    }

    private async Task EnsureBuiltInSystemConfigsAsync(
        SystemConfigRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var seeds = new (string Key, string Value, string Name, string? Remark)[]
        {
            ("security.password.minLength", "8", "密码最小长度", "内置安全策略参数"),
            ("security.lockout.maxFailedAttempts", "5", "最大失败次数", "内置安全策略参数"),
            ("security.lockout.lockMinutes", "15", "锁定时长(分钟)", "内置安全策略参数"),
            ("sys.account.register", "false", "是否开启自助注册", "内置注册策略参数")
        };

        foreach (var seed in seeds)
        {
            var existing = await repository.FindByKeyAsync(tenantId, seed.Key, cancellationToken);
            if (existing is not null)
            {
                continue;
            }

            var entity = new SystemConfig(
                tenantId,
                seed.Key,
                seed.Value,
                seed.Name,
                true,
                idGeneratorAccessor.NextId());
            entity.Update(seed.Value, seed.Name, seed.Remark);
            await repository.AddAsync(entity, cancellationToken);
        }
    }

    private sealed class LegacyAppBindingProjection
    {
        public Guid TenantIdValue { get; set; }

        public long TenantAppInstanceId { get; set; }

        public long DataSourceId { get; set; }

        public long CreatedBy { get; set; }

        public long UpdatedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}





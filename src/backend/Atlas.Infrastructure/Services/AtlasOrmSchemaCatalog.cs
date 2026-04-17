using Atlas.Domain.Alert.Entities;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AgentTeam.Entities;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Assets.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.Plugins;
using Atlas.Domain.Setup.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Domain.Workflow.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// AppHost / PlatformHost 共享的 ORM Schema 目录。
/// 将所有运行时实体集中在一处，避免 setup 初始化、运行时兜底建表和宿主自愈之间出现漂移。
/// </summary>
public static class AtlasOrmSchemaCatalog
{
    private static readonly Type[] AllRuntimeEntityTypes =
    {
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
        typeof(AuditRecord),
        typeof(Asset),
        typeof(AlertRecord),
        typeof(ModelConfig),
        typeof(Agent),
        typeof(AgentPublication),
        typeof(TeamAgent),
        typeof(TeamAgentPublication),
        typeof(TeamAgentTemplate),
        typeof(TeamAgentTemplateMember),
        typeof(TeamAgentConversation),
        typeof(TeamAgentMessage),
        typeof(TeamAgentExecution),
        typeof(TeamAgentExecutionStepEntity),
        typeof(TeamAgentSchemaDraft),
        typeof(TeamAgentSchemaDraftExecutionAudit),
        typeof(AgentTeamDefinition),
        typeof(SubAgentDefinition),
        typeof(OrchestrationNodeDefinition),
        typeof(TeamVersion),
        typeof(ExecutionRun),
        typeof(NodeRun),
        typeof(MultiAgentOrchestration),
        typeof(MultiAgentExecution),
        typeof(MultimodalAsset),
        typeof(EvaluationDataset),
        typeof(EvaluationCase),
        typeof(EvaluationTask),
        typeof(EvaluationResult),
        typeof(OrchestrationPlan),
        typeof(RagExperimentRun),
        typeof(RagShadowComparison),
        typeof(RagFeedback),
        typeof(ApiCallLog),
        typeof(AgentKnowledgeLink),
        typeof(AgentPluginBinding),
        typeof(Conversation),
        typeof(ChatMessage),
        typeof(AgenticRagRunHistory),
        typeof(LongTermMemory),
        typeof(LlmUsageRecord),
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
        typeof(WorkspaceIdeFavorite),
        typeof(AiWorkspace),
        typeof(Workspace),
        typeof(WorkspaceRole),
        typeof(WorkspaceMember),
        typeof(WorkspaceResourcePermission),
        typeof(AiShortcutCommand),
        typeof(AiBotPopupInfo),
        typeof(PersonalAccessToken),
        typeof(OpenApiProject),
        typeof(AuthSession),
        typeof(RefreshToken),
        typeof(ApprovalFlowDefinition),
        typeof(ApprovalFlowDefinitionVersion),
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
        typeof(PersistedWorkflow),
        typeof(PersistedExecutionPointer),
        typeof(PersistedEvent),
        typeof(PersistedSubscription),
        typeof(WorkflowMeta),
        typeof(WorkflowDraft),
        typeof(WorkflowVersion),
        typeof(WorkflowExecution),
        typeof(WorkflowNodeExecution),
        typeof(DictType),
        typeof(DictData),
        typeof(SystemConfig),
        typeof(LoginLog),
        typeof(Notification),
        typeof(UserNotification),
        typeof(FileRecord),
        typeof(FileUploadSession),
        typeof(FileTusUploadSession),
        typeof(AttachmentBinding),
        typeof(TenantDataSource),
        typeof(TenantAppDataSourceBinding),
        typeof(AppDataRoutePolicy),
        typeof(AppMember),
        typeof(AppMemberDepartment),
        typeof(AppMemberPosition),
        typeof(AppRole),
        typeof(AppUserRole),
        typeof(AppRolePermission),
        typeof(AppPermission),
        typeof(AppRolePage),
        typeof(AppDepartment),
        typeof(AppPosition),
        typeof(AppProject),
        typeof(AppProjectUser),
        typeof(Tenant),
        typeof(PluginConfig),
        typeof(PluginMarketEntry),
        typeof(PluginMarketVersion),
        typeof(Atlas.Domain.Templates.ComponentTemplate),
        typeof(Atlas.Domain.Integration.WebhookSubscription),
        typeof(Atlas.Domain.Integration.WebhookDeliveryLog),
        typeof(Atlas.Domain.Integration.ApiConnector),
        typeof(Atlas.Domain.Integration.ApiConnectorOperation),
        typeof(Atlas.Domain.Integration.IntegrationApiKey),
        typeof(Atlas.Domain.License.LicenseRecord),
        typeof(AppManifest),
        typeof(CapabilityManifest),
        typeof(AppBridgeRegistration),
        typeof(AppExposurePolicy),
        typeof(AppCommand),
        typeof(TenantApplication),
        typeof(AppRelease),
        typeof(ReleaseBundle),
        typeof(RuntimeRoute),
        typeof(PackageArtifact),
        typeof(LicenseGrant),
        typeof(ToolAuthorizationPolicy),
        typeof(DataClassification),
        typeof(SensitiveLabel),
        typeof(DlpPolicy),
        typeof(DlpOutboundChannel),
        typeof(LeakageEvent),
        typeof(EvidencePackage),
        typeof(ExternalShareApproval),
        typeof(AppDesignerSnapshot),
        typeof(Atlas.Domain.LogicFlow.Flows.LogicFlowDefinition),
        typeof(Atlas.Domain.LogicFlow.Flows.FlowNodeBinding),
        typeof(Atlas.Domain.LogicFlow.Flows.FlowEdgeDefinition),
        typeof(Atlas.Domain.LogicFlow.Flows.FlowExecution),
        typeof(Atlas.Domain.LogicFlow.Flows.NodeRun),
        typeof(Atlas.Domain.LogicFlow.Expressions.FunctionDefinition),
        typeof(Atlas.Domain.LogicFlow.Expressions.DecisionTableDefinition),
        typeof(Atlas.Domain.LogicFlow.Expressions.RuleChainDefinition),
        typeof(Atlas.Domain.LogicFlow.Nodes.NodeTypeDefinition),
        typeof(Atlas.Domain.LogicFlow.Nodes.NodeTemplate),
        typeof(Atlas.Domain.LogicFlow.Nodes.BusinessTemplateBlock),
        typeof(Atlas.Domain.BatchProcess.Entities.BatchJobDefinition),
        typeof(Atlas.Domain.BatchProcess.Entities.BatchJobExecution),
        typeof(Atlas.Domain.BatchProcess.Entities.ShardExecution),
        typeof(Atlas.Domain.BatchProcess.Entities.BatchExecution),
        typeof(Atlas.Domain.BatchProcess.Entities.BatchDeadLetter),
        typeof(Atlas.Domain.BatchProcess.Entities.BatchCheckpoint),
        typeof(Atlas.Domain.LogicFlow.Flows.LfExecutionLog),
        typeof(Atlas.Domain.LogicFlow.Governance.SysQuota),
        typeof(Atlas.Domain.LogicFlow.Governance.SysCanaryRelease),
        typeof(Atlas.Domain.LogicFlow.Governance.SysVersionFreeze),
        // Coze PRD Phase III - M2: 工作空间文件夹与发布渠道
        typeof(WorkspaceFolder),
        typeof(WorkspacePublishChannel),
        // Coze PRD Phase III - M4.2: 文件夹与对象的关联表
        typeof(WorkspaceFolderItem),
        // Coze PRD Phase III - M4.5: 平台运营内容（首页 banner / tutorial / announcement / recommended）
        typeof(PlatformContent),
        // Coze PRD Phase III - M6.3: 用户级 KV 设置（per-user 偏好持久化）
        typeof(UserSetting),
        // 系统初始化与迁移控制台（Setup Console）8 张元数据表
        typeof(SystemSetupState),
        typeof(WorkspaceSetupState),
        typeof(SetupStepRecord),
        typeof(DataMigrationJob),
        typeof(DataMigrationBatch),
        typeof(DataMigrationCheckpoint),
        typeof(DataMigrationLog),
        typeof(DataMigrationReport),
        // M8 新增：ConsoleToken 持久化（A3） + 种子 bundle 应用日志（B1）
        typeof(SetupConsoleToken),
        typeof(SetupSeedBundleLog),
        // M01 低代码 UI Builder：7 张表（PLAN.md §M01 S01-1）
        typeof(AppDefinition),
        typeof(PageDefinition),
        typeof(AppVariable),
        typeof(AppContentParam),
        typeof(AppVersionArchive),
        typeof(AppPublishArtifact),
        typeof(AppResourceReference)
    };

    private static readonly Type[] CriticalAppSetupEntityTypes =
    {
        typeof(UserAccount),
        typeof(Role),
        typeof(UserRole),
        typeof(AuditRecord),
        typeof(LoginLog),
        typeof(AuthSession),
        typeof(RefreshToken),
        typeof(AppDataRoutePolicy),
        typeof(AppRole),
        typeof(AppRolePermission),
        typeof(AppPermission),
        typeof(AppDepartment),
        typeof(AppPosition),
        typeof(AppMember)
    };

    public static IReadOnlyList<Type> RuntimeEntities => AllRuntimeEntityTypes;

    public static void EnsureRuntimeSchema(ISqlSugarClient db)
    {
        ArgumentNullException.ThrowIfNull(db);
        db.CodeFirst.InitTables(AllRuntimeEntityTypes);
    }

    public static IReadOnlyList<string> GetMissingCriticalTableNames(ISqlSugarClient db)
    {
        ArgumentNullException.ThrowIfNull(db);

        var missing = new List<string>();
        foreach (var entityType in CriticalAppSetupEntityTypes)
        {
            var tableName = db.EntityMaintenance.GetTableName(entityType);
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
                missing.Add(tableName);
            }
        }

        return missing;
    }
}

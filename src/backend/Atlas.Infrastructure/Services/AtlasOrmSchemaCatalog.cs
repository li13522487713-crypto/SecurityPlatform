using Atlas.Domain.Alert.Entities;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Entities.Channels;
using Atlas.Domain.AiPlatform.Entities.Knowledge;
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
using Atlas.Domain.ExternalConnectors.Entities;
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
        typeof(VoiceAsset),
        typeof(LlmUsageRecord),
        typeof(KnowledgeBase),
        typeof(KnowledgeDocument),
        typeof(DocumentChunk),
        typeof(KnowledgeSlice),
        typeof(KnowledgeReview),
        typeof(KnowledgeImportTask),
        // v5 §32-44 / 计划 G2：知识库专题扩展实体（去重 + 新增 ParseJob/IndexJob/KnowledgeTable/DocumentVersion）
        typeof(KnowledgeBaseMetaEntity),
        typeof(KnowledgeDocumentMetaEntity),
        typeof(KnowledgeJob),
        typeof(KnowledgeParseJob),
        typeof(KnowledgeIndexJob),
        typeof(KnowledgeBindingEntity),
        typeof(KnowledgePermissionEntity),
        typeof(KnowledgeDocumentVersion),
        typeof(KnowledgeRetrievalLogEntity),
        typeof(KnowledgeProviderConfigEntity),
        typeof(KnowledgeTable),
        typeof(KnowledgeTableColumnEntity),
        typeof(KnowledgeTableRowEntity),
        typeof(KnowledgeImageItemEntity),
        typeof(KnowledgeImageAnnotationEntity),
        typeof(AiWorkflowDefinition),
        typeof(AiDatabase),
        typeof(AiDatabaseHostProfile),
        typeof(AiDatabasePhysicalInstance),
        typeof(AiDatabaseField),
        typeof(AiDatabaseChannelConfig),
        typeof(AiDatabaseRecord),
        typeof(AiDatabaseImportTask),
        typeof(AiVariable),
        typeof(AiPlugin),
        typeof(AiPluginApi),
        typeof(AiApp),
        typeof(AiAppPublishRecord),
        typeof(AiAppConversationTemplate),
        typeof(AiAppResourceBinding),
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
        // External Collaboration Connector 11 张表（v4 报告 27-31 章 P0 落地）
        typeof(ExternalIdentityProvider),
        typeof(ExternalIdentityBinding),
        typeof(ExternalIdentityBindingAuditLog),
        typeof(ExternalDepartmentMirror),
        typeof(ExternalUserMirror),
        typeof(ExternalDepartmentUserRelation),
        typeof(LocalDepartmentMapping),
        typeof(ExternalDirectorySyncJob),
        typeof(ExternalDirectorySyncDiff),
        typeof(ExternalApprovalTemplateCache),
        typeof(ExternalApprovalTemplateMapping),
        typeof(ExternalApprovalInstanceLink),
        typeof(ExternalMessageDispatch),
        typeof(ExternalCallbackEvent),
        typeof(PersistedWorkflow),
        typeof(PersistedExecutionPointer),
        typeof(PersistedEvent),
        typeof(PersistedSubscription),
        typeof(WorkflowMeta),
        typeof(WorkflowDraft),
        typeof(WorkflowVersion),
        typeof(CozeWorkflowMeta),
        typeof(CozeWorkflowDraft),
        typeof(CozeWorkflowVersion),
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
        // 治理 M-G02-C2 (S1): 渠道发布版本与回滚
        typeof(WorkspaceChannelRelease),
        // 治理 M-G02-C5 (S3): 飞书渠道凭据
        typeof(FeishuChannelCredential),
        // 治理 M-G02-C9 (S4): 微信公众号渠道凭据
        typeof(WechatMpChannelCredential),
        // 渠道凭据扩展：微信小程序 / 微信客服
        typeof(WechatMiniappChannelCredential),
        typeof(WechatCsChannelCredential),
        // 治理 M-G05-C1 (S9): 组织实体
        typeof(Organization),
        // 治理 M-G05-C4 (S10): 组织成员
        typeof(OrganizationMember),
        // 治理 M-G06-C1 (S11): 成员邀请
        typeof(MemberInvitation),
        // 治理 M-G06-C3 (S12): 资源所有权移交
        typeof(ResourceOwnershipTransfer),
        // 治理 M-G07-C2 (S13): 租户级身份提供方
        typeof(TenantIdentityProvider),
        // 治理 M-G08-C1 + C2 (S15): 网络策略 + 数据驻留策略
        typeof(TenantNetworkPolicy),
        typeof(TenantDataResidencyPolicy),
        // 治理 M-G10-C1 + C2 (S16): Agent 触发器与卡片
        typeof(AgentTrigger),
        typeof(AgentCard),
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
        typeof(DataMigrationTableProgress),
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
        typeof(AppResourceReference),
        typeof(AppDraftLock),
        typeof(AppComponentOverride),
        typeof(RuntimeWorkflowAsyncJob),
        typeof(LowCodeAssetUploadSession),
        typeof(LowCodeSession),
        typeof(LowCodeMessageLogEntry),
        typeof(LowCodeTrigger),
        typeof(LowCodeWebviewDomain),
        typeof(RuntimeTrace),
        typeof(RuntimeSpan),
        typeof(AppFaqEntry),
        typeof(AppPromptTemplate),
        typeof(LowCodePluginDefinition),
        typeof(LowCodePluginVersion),
        typeof(LowCodePluginAuthorization),
        typeof(LowCodePluginUsage),
        typeof(NodeStateEntry),
        typeof(AppTemplate)
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
        EnsureAiDatabaseManagementSchema(db);
        db.CodeFirst.InitTables(AllRuntimeEntityTypes);
    }

    private static void EnsureAiDatabaseManagementSchema(ISqlSugarClient db)
    {
        if (db.CurrentConnectionConfig?.DbType != DbType.Sqlite
            || !db.DbMaintenance.IsAnyTable("AiDatabase", false))
        {
            return;
        }

        AddColumnIfMissing(db, "AiDatabase", "StorageMode", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(db, "AiDatabase", "DriverCode", "TEXT NOT NULL DEFAULT 'SQLite'");
        AddColumnIfMissing(db, "AiDatabase", "EncryptedDraftConnection", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "AiDatabase", "EncryptedOnlineConnection", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "AiDatabase", "PhysicalDatabaseName", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "AiDatabase", "DraftDatabaseName", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "AiDatabase", "OnlineDatabaseName", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "AiDatabase", "DefaultHostProfileId", "INTEGER NULL");
        AddColumnIfMissing(db, "AiDatabase", "DraftInstanceId", "INTEGER NULL");
        AddColumnIfMissing(db, "AiDatabase", "OnlineInstanceId", "INTEGER NULL");
        AddColumnIfMissing(db, "AiDatabase", "DialectVersion", "TEXT NOT NULL DEFAULT 'v1'");
        AddColumnIfMissing(db, "AiDatabase", "ProvisionState", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "AiDatabase", "ProvisionError", "TEXT NULL");

        if (SqliteSchemaAlignment.RequiresNullableColumnFix<AiDatabaseHostProfile>(
                db,
                "Port",
                "MaxDatabaseCount",
                "LastTestAt",
                "LastTestMessage"))
        {
            SqliteSchemaAlignment.RebuildTableViaOrm<AiDatabaseHostProfile>(db);
        }

        if (SqliteSchemaAlignment.RequiresNullableColumnFix<AiDatabasePhysicalInstance>(
                db,
                "ProvisionError",
                "DriverVersion",
                "LastConnectedAt",
                "LastConnectionTestMessage"))
        {
            SqliteSchemaAlignment.RebuildTableViaOrm<AiDatabasePhysicalInstance>(db);
        }
    }

    private static void AddColumnIfMissing(ISqlSugarClient db, string tableName, string columnName, string columnDefinition)
    {
        var columns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false);
        if (columns.Any(c => string.Equals(c.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        db.Ado.ExecuteCommand($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
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

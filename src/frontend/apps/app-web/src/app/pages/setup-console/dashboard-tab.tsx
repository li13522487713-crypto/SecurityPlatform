import { useCallback, useState } from "react";
import { Button, Collapse, Table, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useAppI18n } from "../../i18n";
import { getSetupConsoleCatalogEntities } from "../../../services/mock";
import type {
  SetupConsoleOverviewDto,
  SystemSetupStateDto,
  WorkspaceSetupStateDto,
  DataMigrationJobDto
} from "../../../services/api-setup-console";
import type { AppMessageKey } from "../../messages";
import {
  isMigrationDone,
  isSystemBusy,
  isSystemInitDone,
  shouldShowResumeBanner,
  type DataMigrationState,
  type SystemSetupState,
  type WorkspaceSetupState
} from "../../setup-console-state-machine";
import {
  InfoBanner,
  SectionCard,
  StateBadge,
  type StateBadgeVariant
} from "../../_shared";

const { Text } = Typography;

interface DashboardTabProps {
  overview: SetupConsoleOverviewDto | null;
  loading: boolean;
  refreshing: boolean;
  onRefresh: () => Promise<void>;
  onJumpToTab: (tab: "system-init" | "workspace-init" | "migration") => void;
}

const SYSTEM_STATE_LABEL_KEY: Record<SystemSetupState, AppMessageKey> = {
  not_started: "setupConsoleStateNotStarted",
  precheck_passed: "setupConsoleStatePrecheckPassed",
  schema_initializing: "setupConsoleStateSchemaInitializing",
  schema_initialized: "setupConsoleStateSchemaInitialized",
  seed_initializing: "setupConsoleStateSeedInitializing",
  seed_initialized: "setupConsoleStateSeedInitialized",
  migration_pending: "setupConsoleStateMigrationPending",
  migration_running: "setupConsoleStateMigrationRunning",
  migration_partially_completed: "setupConsoleStateMigrationPartiallyCompleted",
  migration_completed: "setupConsoleStateMigrationCompleted",
  validation_running: "setupConsoleStateValidationRunning",
  completed: "setupConsoleStateCompleted",
  failed: "setupConsoleStateFailed",
  dismissed: "setupConsoleStateDismissed"
};

const WORKSPACE_STATE_LABEL_KEY: Record<WorkspaceSetupState, AppMessageKey> = {
  workspace_init_pending: "setupConsoleStateWorkspaceInitPending",
  workspace_init_running: "setupConsoleStateWorkspaceInitRunning",
  workspace_init_completed: "setupConsoleStateWorkspaceInitCompleted",
  workspace_init_failed: "setupConsoleStateWorkspaceInitFailed"
};

const MIGRATION_STATE_LABEL_KEY: Record<DataMigrationState, AppMessageKey> = {
  created: "setupConsoleMigrationStatePending",
  pending: "setupConsoleMigrationStatePending",
  prechecking: "setupConsoleMigrationStatePrechecking",
  ready: "setupConsoleMigrationStateReady",
  queued: "setupConsoleMigrationStatePending",
  running: "setupConsoleMigrationStateRunning",
  cancelling: "setupConsoleMigrationStateRunning",
  cancelled: "setupConsoleMigrationStateFailed",
  succeeded: "setupConsoleMigrationStateCutoverCompleted",
  validating: "setupConsoleMigrationStateValidating",
  validation_failed: "setupConsoleMigrationStateFailed",
  validated: "setupConsoleMigrationStateCutoverReady",
  "cutover-ready": "setupConsoleMigrationStateCutoverReady",
  "cutover-completed": "setupConsoleMigrationStateCutoverCompleted",
  "cutover-failed": "setupConsoleMigrationStateFailed",
  failed: "setupConsoleMigrationStateFailed",
  "rolled-back": "setupConsoleMigrationStateRolledBack"
};

export function DashboardTab({ overview, loading, refreshing, onRefresh, onJumpToTab }: DashboardTabProps) {
  const { t } = useAppI18n();

  if (loading || !overview) {
    return (
      <SectionCard testId="setup-console-dashboard-loading">
        <Text type="tertiary">{t("loading")}</Text>
      </SectionCard>
    );
  }

  return (
    <div data-testid="setup-console-dashboard">
      <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: 16 }}>
        <Button
          type="tertiary"
          theme="light"
          data-testid="setup-console-dashboard-refresh"
          loading={refreshing}
          onClick={() => void onRefresh()}
        >
          {refreshing ? t("setupConsoleRefreshing") : t("setupConsoleRefresh")}
        </Button>
      </div>

      <SystemCard system={overview.system} onJumpToTab={onJumpToTab} />
      <WorkspaceCard workspaces={overview.workspaces} onJumpToTab={onJumpToTab} />
      <MigrationCard migration={overview.activeMigration} onJumpToTab={onJumpToTab} />
      <CatalogCard catalog={overview.catalogSummary} />
    </div>
  );
}

function systemStateVariant(state: SystemSetupState): StateBadgeVariant {
  if (isSystemInitDone(state)) {
    return "success";
  }
  if (isSystemBusy(state)) {
    return "info";
  }
  if (shouldShowResumeBanner(state)) {
    return "danger";
  }
  return "neutral";
}

function SystemCard({
  system,
  onJumpToTab
}: {
  system: SystemSetupStateDto;
  onJumpToTab: (tab: "system-init" | "workspace-init" | "migration") => void;
}) {
  const { t } = useAppI18n();
  const stateLabel = t(SYSTEM_STATE_LABEL_KEY[system.state]);
  const showResume = shouldShowResumeBanner(system.state);

  return (
    <div data-testid="setup-console-system-card">
      <SectionCard
        title={t("setupConsoleSystemSectionTitle")}
        subtitle={`${t("setupConsoleSystemLastUpdatedLabel")}: ${system.lastUpdatedAt}`}
        actions={
          <StateBadge
            variant={systemStateVariant(system.state)}
            testId="setup-console-system-state-badge"
          >
            {stateLabel}
          </StateBadge>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          <Text type="tertiary">
            {t("setupConsoleSystemVersionLabel")}: {system.version}
          </Text>
          <Text type="tertiary">
            {t("setupConsoleSystemRecoveryConfiguredLabel")}:{" "}
            <span data-testid="setup-console-system-recovery-status">
              {system.recoveryKeyConfigured ? t("setupConsoleBooleanTrue") : t("setupConsoleBooleanFalse")}
            </span>
          </Text>
          {system.failureMessage ? (
            <Text type="tertiary" data-testid="setup-console-system-failure-message">
              {t("setupConsoleSystemFailureMessageLabel")}: {system.failureMessage}
            </Text>
          ) : null}
        </div>

        {showResume ? (
          <div style={{ marginTop: 12 }}>
            <InfoBanner
              variant="warning"
              compact
              title={t("setupConsoleSystemResumeBannerTitle")}
              description={t("setupConsoleSystemResumeBannerDesc")}
              testId="setup-console-system-resume-banner"
            />
          </div>
        ) : null}

        <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 16 }}>
          <Button
            type="primary"
            theme="solid"
            data-testid="setup-console-system-jump"
            onClick={() => onJumpToTab("system-init")}
          >
            {t("setupConsoleTabSystemInit")}
          </Button>
        </div>
      </SectionCard>
    </div>
  );
}

function WorkspaceCard({
  workspaces,
  onJumpToTab
}: {
  workspaces: WorkspaceSetupStateDto[];
  onJumpToTab: (tab: "system-init" | "workspace-init" | "migration") => void;
}) {
  const { t } = useAppI18n();

  const columns: ColumnProps<WorkspaceSetupStateDto>[] = [
    { title: t("setupConsoleWorkspaceColumnId"), dataIndex: "workspaceId" },
    { title: t("setupConsoleWorkspaceColumnName"), dataIndex: "workspaceName" },
    {
      title: t("setupConsoleWorkspaceColumnState"),
      dataIndex: "state",
      render: (_value, record) => (
        <StateBadge variant="info">{t(WORKSPACE_STATE_LABEL_KEY[record.state])}</StateBadge>
      )
    },
    { title: t("setupConsoleWorkspaceColumnVersion"), dataIndex: "seedBundleVersion" },
    { title: t("setupConsoleWorkspaceColumnLastUpdated"), dataIndex: "lastUpdatedAt" }
  ];

  return (
    <div data-testid="setup-console-workspace-card">
      <SectionCard
        title={t("setupConsoleWorkspaceSectionTitle")}
        actions={
          <Button
            type="tertiary"
            theme="light"
            data-testid="setup-console-workspace-jump"
            onClick={() => onJumpToTab("workspace-init")}
          >
            {t("setupConsoleTabWorkspaceInit")}
          </Button>
        }
      >
        {workspaces.length === 0 ? (
          <Text type="tertiary">{t("setupConsoleWorkspaceEmpty")}</Text>
        ) : (
          <Table
            data-testid="setup-console-workspace-table"
            columns={columns}
            dataSource={workspaces.map((workspace) => ({
              ...workspace,
              key: workspace.workspaceId
            }))}
            pagination={false}
            size="small"
            onRow={(record) =>
              record
                ? {
                    "data-testid": `setup-console-workspace-row-${record.workspaceId}`
                  }
                : {}
            }
          />
        )}
      </SectionCard>
    </div>
  );
}

function MigrationCard({
  migration,
  onJumpToTab
}: {
  migration: DataMigrationJobDto | null;
  onJumpToTab: (tab: "system-init" | "workspace-init" | "migration") => void;
}) {
  const { t } = useAppI18n();
  const migrationState = migration?.state as DataMigrationState | undefined;

  return (
    <div data-testid="setup-console-migration-card">
      <SectionCard
        title={t("setupConsoleMigrationSectionTitle")}
        actions={
          <Button
            type="tertiary"
            theme="light"
            data-testid="setup-console-migration-jump"
            onClick={() => onJumpToTab("migration")}
          >
            {t("setupConsoleTabMigration")}
          </Button>
        }
      >
        {migration ? (
          <div data-testid="setup-console-migration-active" style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <Text type="tertiary">
              {t("setupConsoleMigrationColumnId")}: <code>{migration.id}</code>
            </Text>
            <Text type="tertiary">
              {t("setupConsoleMigrationColumnState")}:{" "}
              <StateBadge
                variant={isMigrationDone(migration.state as DataMigrationState) ? "success" : "info"}
                testId="setup-console-migration-state-badge"
              >
                {t(migrationState ? MIGRATION_STATE_LABEL_KEY[migrationState] : "setupConsoleMigrationStatePending")}
              </StateBadge>
            </Text>
            <Text type="tertiary">
              {t("setupConsoleMigrationColumnProgress")}: {migration.progressPercent}%
            </Text>
          </div>
        ) : (
          <Text type="tertiary">{t("setupConsoleMigrationEmpty")}</Text>
        )}
      </SectionCard>
    </div>
  );
}

function CatalogCard({ catalog }: { catalog: SetupConsoleOverviewDto["catalogSummary"] }) {
  const { t } = useAppI18n();
  const [activeKeys, setActiveKeys] = useState<string[]>([]);
  const [details, setDetails] = useState<Record<string, readonly string[]>>({});
  const [loading, setLoading] = useState<string | null>(null);

  const handleChange = useCallback(
    async (keys: string | string[] | undefined) => {
      const next = Array.isArray(keys) ? keys : keys ? [keys] : [];
      setActiveKeys(next);
      const newlyOpened = next.find((key) => !details[key]);
      if (!newlyOpened) {
        return;
      }
      setLoading(newlyOpened);
      try {
        const response = await getSetupConsoleCatalogEntities(newlyOpened);
        if (response.success && response.data) {
          setDetails((previous) => ({ ...previous, [newlyOpened]: response.data ?? [] }));
        }
      } finally {
        setLoading(null);
      }
    },
    [details]
  );

  return (
    <div data-testid="setup-console-catalog-card">
      <SectionCard title={t("setupConsoleCatalogSectionTitle")}>
        <Text type="tertiary" style={{ display: "block", marginBottom: 12 }}>
          {t("setupConsoleCatalogTotalLabel")}: {catalog.totalEntities} ({catalog.totalCategories}{" "}
          {t("setupConsoleCatalogCategoryUnit")})
        </Text>
        <Collapse activeKey={activeKeys} onChange={(keys) => void handleChange(keys)}>
          {catalog.categories.map((category) => {
            const list = details[category.category];
            const isLoading = loading === category.category;
            return (
              <Collapse.Panel
                key={category.category}
                itemKey={category.category}
                header={
                  <span data-testid={`setup-console-catalog-row-${category.category}`}>
                    <span data-testid={`setup-console-catalog-toggle-${category.category}`}>
                      {t(category.displayKey as AppMessageKey)} — {category.entityCount}
                      {category.hasSeed ? ` (${t("setupConsoleBooleanTrue")})` : ""}
                    </span>
                  </span>
                }
              >
                {isLoading ? (
                  <Text type="tertiary">{t("loading")}</Text>
                ) : (
                  <ul
                    style={{ paddingLeft: 16, margin: 0 }}
                    data-testid={`setup-console-catalog-details-${category.category}`}
                  >
                    {(list ?? []).map((entityName) => (
                      <li key={entityName}>
                        <code>{entityName}</code>
                      </li>
                    ))}
                  </ul>
                )}
              </Collapse.Panel>
            );
          })}
        </Collapse>
        {catalog.missingCriticalTables.length > 0 ? (
          <div style={{ marginTop: 12 }}>
            <InfoBanner
              variant="warning"
              compact
              title={t("setupConsoleCatalogMissingLabel")}
              description={catalog.missingCriticalTables.join(", ")}
              testId="setup-console-catalog-missing"
            />
          </div>
        ) : null}
      </SectionCard>
    </div>
  );
}

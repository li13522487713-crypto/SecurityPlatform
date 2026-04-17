import { useCallback, useState } from "react";
import { useAppI18n } from "../../i18n";
import { getSetupConsoleCatalogEntities } from "../../../services/mock";
import type {
  SetupConsoleOverviewDto,
  SystemSetupStateDto,
  WorkspaceSetupStateDto,
  DataMigrationJobDto
} from "../../../services/api-setup-console";
import type {
  AppMessageKey
} from "../../messages";
import {
  isMigrationDone,
  isSystemBusy,
  isSystemInitDone,
  shouldShowResumeBanner,
  type DataMigrationState,
  type SystemSetupState,
  type WorkspaceSetupState
} from "../../setup-console-state-machine";

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
  pending: "setupConsoleMigrationStatePending",
  prechecking: "setupConsoleMigrationStatePrechecking",
  ready: "setupConsoleMigrationStateReady",
  running: "setupConsoleMigrationStateRunning",
  validating: "setupConsoleMigrationStateValidating",
  "cutover-ready": "setupConsoleMigrationStateCutoverReady",
  "cutover-completed": "setupConsoleMigrationStateCutoverCompleted",
  failed: "setupConsoleMigrationStateFailed",
  "rolled-back": "setupConsoleMigrationStateRolledBack"
};

export function DashboardTab({ overview, loading, refreshing, onRefresh, onJumpToTab }: DashboardTabProps) {
  const { t } = useAppI18n();

  if (loading || !overview) {
    return (
      <section className="atlas-setup-panel" data-testid="setup-console-dashboard-loading">
        <p className="atlas-field-hint">{t("loading")}</p>
      </section>
    );
  }

  return (
    <div data-testid="setup-console-dashboard">
      <div className="atlas-setup-actions" style={{ marginBottom: 16 }}>
        <span />
        <button
          type="button"
          className="atlas-button atlas-button--secondary"
          data-testid="setup-console-dashboard-refresh"
          disabled={refreshing}
          onClick={() => void onRefresh()}
        >
          {refreshing ? t("setupConsoleRefreshing") : t("setupConsoleRefresh")}
        </button>
      </div>

      <SystemCard system={overview.system} onJumpToTab={onJumpToTab} />
      <WorkspaceCard workspaces={overview.workspaces} onJumpToTab={onJumpToTab} />
      <MigrationCard migration={overview.activeMigration} onJumpToTab={onJumpToTab} />
      <CatalogCard catalog={overview.catalogSummary} />
    </div>
  );
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
  const done = isSystemInitDone(system.state);
  const busy = isSystemBusy(system.state);

  return (
    <section className="atlas-setup-panel" data-testid="setup-console-system-card">
      <div className="atlas-org-section__header">
        <div>
          <div className="atlas-section-title">{t("setupConsoleSystemSectionTitle")}</div>
          <div className="atlas-field-hint">{t("setupConsoleSystemLastUpdatedLabel")}: {system.lastUpdatedAt}</div>
        </div>
        <div>
          <span
            className={`atlas-pill ${done ? "is-success" : busy ? "is-info" : showResume ? "is-error" : ""}`.trim()}
            data-testid="setup-console-system-state-badge"
          >
            {stateLabel}
          </span>
        </div>
      </div>
      <div className="atlas-form-grid">
        <p className="atlas-field-hint">{t("setupConsoleSystemVersionLabel")}: {system.version}</p>
        <p className="atlas-field-hint">
          {t("setupConsoleSystemRecoveryConfiguredLabel")}:{" "}
          <span data-testid="setup-console-system-recovery-status">
            {system.recoveryKeyConfigured ? t("setupConsoleBooleanTrue") : t("setupConsoleBooleanFalse")}
          </span>
        </p>
        {system.failureMessage ? (
          <p className="atlas-field-hint" data-testid="setup-console-system-failure-message">
            {t("setupConsoleSystemFailureMessageLabel")}: {system.failureMessage}
          </p>
        ) : null}
      </div>
      {showResume ? (
        <div className="atlas-warning-banner" data-testid="setup-console-system-resume-banner">
          <strong>{t("setupConsoleSystemResumeBannerTitle")}</strong>
          <p>{t("setupConsoleSystemResumeBannerDesc")}</p>
        </div>
      ) : null}
      <div className="atlas-setup-actions">
        <span />
        <button
          type="button"
          className="atlas-button atlas-button--primary"
          data-testid="setup-console-system-jump"
          onClick={() => onJumpToTab("system-init")}
        >
          {t("setupConsoleTabSystemInit")}
        </button>
      </div>
    </section>
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

  return (
    <section className="atlas-setup-panel" data-testid="setup-console-workspace-card">
      <div className="atlas-org-section__header">
        <div>
          <div className="atlas-section-title">{t("setupConsoleWorkspaceSectionTitle")}</div>
        </div>
        <button
          type="button"
          className="atlas-button atlas-button--secondary"
          data-testid="setup-console-workspace-jump"
          onClick={() => onJumpToTab("workspace-init")}
        >
          {t("setupConsoleTabWorkspaceInit")}
        </button>
      </div>
      {workspaces.length === 0 ? (
        <p className="atlas-field-hint">{t("setupConsoleWorkspaceEmpty")}</p>
      ) : (
        <table className="atlas-table" data-testid="setup-console-workspace-table">
          <thead>
            <tr>
              <th>{t("setupConsoleWorkspaceColumnId")}</th>
              <th>{t("setupConsoleWorkspaceColumnName")}</th>
              <th>{t("setupConsoleWorkspaceColumnState")}</th>
              <th>{t("setupConsoleWorkspaceColumnVersion")}</th>
              <th>{t("setupConsoleWorkspaceColumnLastUpdated")}</th>
            </tr>
          </thead>
          <tbody>
            {workspaces.map((workspace) => (
              <tr key={workspace.workspaceId} data-testid={`setup-console-workspace-row-${workspace.workspaceId}`}>
                <td>{workspace.workspaceId}</td>
                <td>{workspace.workspaceName}</td>
                <td>
                  <span className="atlas-pill">{t(WORKSPACE_STATE_LABEL_KEY[workspace.state])}</span>
                </td>
                <td>{workspace.seedBundleVersion}</td>
                <td>{workspace.lastUpdatedAt}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
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

  return (
    <section className="atlas-setup-panel" data-testid="setup-console-migration-card">
      <div className="atlas-org-section__header">
        <div>
          <div className="atlas-section-title">{t("setupConsoleMigrationSectionTitle")}</div>
        </div>
        <button
          type="button"
          className="atlas-button atlas-button--secondary"
          data-testid="setup-console-migration-jump"
          onClick={() => onJumpToTab("migration")}
        >
          {t("setupConsoleTabMigration")}
        </button>
      </div>
      {migration ? (
        <div data-testid="setup-console-migration-active">
          <p className="atlas-field-hint">
            {t("setupConsoleMigrationColumnId")}: <code>{migration.id}</code>
          </p>
          <p className="atlas-field-hint">
            {t("setupConsoleMigrationColumnState")}:{" "}
            <span
              className={`atlas-pill ${isMigrationDone(migration.state) ? "is-success" : "is-info"}`.trim()}
              data-testid="setup-console-migration-state-badge"
            >
              {t(MIGRATION_STATE_LABEL_KEY[migration.state])}
            </span>
          </p>
          <p className="atlas-field-hint">
            {t("setupConsoleMigrationColumnProgress")}: {migration.progressPercent}%
          </p>
        </div>
      ) : (
        <p className="atlas-field-hint">{t("setupConsoleMigrationEmpty")}</p>
      )}
    </section>
  );
}

function CatalogCard({ catalog }: { catalog: SetupConsoleOverviewDto["catalogSummary"] }) {
  const { t } = useAppI18n();
  const [expanded, setExpanded] = useState<string | null>(null);
  const [details, setDetails] = useState<Record<string, readonly string[]>>({});
  const [loading, setLoading] = useState<string | null>(null);

  const toggle = useCallback(
    async (categoryId: string) => {
      if (expanded === categoryId) {
        setExpanded(null);
        return;
      }
      setExpanded(categoryId);
      if (details[categoryId]) {
        return;
      }
      setLoading(categoryId);
      try {
        const response = await getSetupConsoleCatalogEntities(categoryId);
        if (response.success && response.data) {
          setDetails((previous) => ({ ...previous, [categoryId]: response.data ?? [] }));
        }
      } finally {
        setLoading(null);
      }
    },
    [details, expanded]
  );

  return (
    <section className="atlas-setup-panel" data-testid="setup-console-catalog-card">
      <div className="atlas-section-title">{t("setupConsoleCatalogSectionTitle")}</div>
      <p className="atlas-field-hint">
        {t("setupConsoleCatalogTotalLabel")}: {catalog.totalEntities} ({catalog.totalCategories}{" "}
        {t("setupConsoleCatalogCategoryUnit")})
      </p>
      <ul>
        {catalog.categories.map((category) => {
          const isOpen = expanded === category.category;
          const list = details[category.category];
          return (
            <li key={category.category} data-testid={`setup-console-catalog-row-${category.category}`}>
              <button
                type="button"
                className="atlas-button atlas-button--text"
                style={{ background: "none", border: "none", cursor: "pointer", padding: 0, color: "inherit" }}
                onClick={() => void toggle(category.category)}
                data-testid={`setup-console-catalog-toggle-${category.category}`}
              >
                {isOpen ? "▼ " : "▶ "}
                {t(category.displayKey as AppMessageKey)} — {category.entityCount}
                {category.hasSeed ? ` (${t("setupConsoleBooleanTrue")})` : ""}
              </button>
              {isOpen ? (
                loading === category.category ? (
                  <p className="atlas-field-hint" style={{ marginLeft: 16 }}>
                    {t("loading")}
                  </p>
                ) : (
                  <ul style={{ marginLeft: 16 }} data-testid={`setup-console-catalog-details-${category.category}`}>
                    {(list ?? []).map((entityName) => (
                      <li key={entityName}>
                        <code>{entityName}</code>
                      </li>
                    ))}
                  </ul>
                )
              ) : null}
            </li>
          );
        })}
      </ul>
      {catalog.missingCriticalTables.length > 0 ? (
        <div className="atlas-warning-banner" data-testid="setup-console-catalog-missing">
          <strong>{t("setupConsoleCatalogMissingLabel")}</strong>
          <p>{catalog.missingCriticalTables.join(", ")}</p>
        </div>
      ) : null}
    </section>
  );
}

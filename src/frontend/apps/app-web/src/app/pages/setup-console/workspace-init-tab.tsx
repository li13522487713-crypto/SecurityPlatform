import { useCallback, useState } from "react";
import { Button, Space, Table, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useAppI18n } from "../../i18n";
import { useBootstrap } from "../../bootstrap-context";
import {
  applyWorkspaceSeedBundle,
  completeWorkspaceInit,
  initializeWorkspace
} from "../../../services/mock";
import type { WorkspaceSetupStateDto } from "../../../services/api-setup-console";
import type { AppMessageKey } from "../../messages";
import {
  isWorkspaceInitDone,
  type WorkspaceSetupState
} from "../../setup-console-state-machine";
import {
  InfoBanner,
  SectionCard,
  StateBadge,
  type StateBadgeVariant
} from "../../_shared";

const { Text } = Typography;

interface WorkspaceInitTabProps {
  workspaces: WorkspaceSetupStateDto[];
  onSnapshotChanged: () => Promise<void>;
}

const WORKSPACE_STATE_LABEL_KEY: Record<WorkspaceSetupState, AppMessageKey> = {
  workspace_init_pending: "setupConsoleStateWorkspaceInitPending",
  workspace_init_running: "setupConsoleStateWorkspaceInitRunning",
  workspace_init_completed: "setupConsoleStateWorkspaceInitCompleted",
  workspace_init_failed: "setupConsoleStateWorkspaceInitFailed"
};

const WORKSPACE_STATE_VARIANT: Record<WorkspaceSetupState, StateBadgeVariant> = {
  workspace_init_pending: "neutral",
  workspace_init_running: "info",
  workspace_init_completed: "success",
  workspace_init_failed: "danger"
};

export function WorkspaceInitTab({ workspaces, onSnapshotChanged }: WorkspaceInitTabProps) {
  const { t } = useAppI18n();
  const { refreshSetupConsole } = useBootstrap();
  const [busyWorkspaceId, setBusyWorkspaceId] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const refreshAll = useCallback(async () => {
    await refreshSetupConsole();
    await onSnapshotChanged();
  }, [onSnapshotChanged, refreshSetupConsole]);

  const initialize = useCallback(
    async (workspace: WorkspaceSetupStateDto) => {
      setBusyWorkspaceId(workspace.workspaceId);
      setErrorMessage(null);
      try {
        await initializeWorkspace(workspace.workspaceId, {
          workspaceName: workspace.workspaceName,
          seedBundleVersion: "v1",
          applyDefaultRoles: true,
          applyDefaultPublishChannels: true
        });
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : t("setupConsoleStepStateFailed"));
      } finally {
        setBusyWorkspaceId(null);
        await refreshAll();
      }
    },
    [refreshAll, t]
  );

  const applyBundle = useCallback(
    async (workspace: WorkspaceSetupStateDto) => {
      setBusyWorkspaceId(workspace.workspaceId);
      setErrorMessage(null);
      try {
        await applyWorkspaceSeedBundle(workspace.workspaceId, {
          bundleVersion: "v1",
          forceReapply: true
        });
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : t("setupConsoleStepStateFailed"));
      } finally {
        setBusyWorkspaceId(null);
        await refreshAll();
      }
    },
    [refreshAll, t]
  );

  const complete = useCallback(
    async (workspace: WorkspaceSetupStateDto) => {
      setBusyWorkspaceId(workspace.workspaceId);
      setErrorMessage(null);
      try {
        await completeWorkspaceInit(workspace.workspaceId);
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : t("setupConsoleStepStateFailed"));
      } finally {
        setBusyWorkspaceId(null);
        await refreshAll();
      }
    },
    [refreshAll, t]
  );

  const columns: ColumnProps<WorkspaceSetupStateDto>[] = [
    { title: t("setupConsoleWorkspaceColumnId"), dataIndex: "workspaceId" },
    { title: t("setupConsoleWorkspaceColumnName"), dataIndex: "workspaceName" },
    {
      title: t("setupConsoleWorkspaceColumnState"),
      dataIndex: "state",
      render: (_value, record) => (
        <StateBadge
          variant={WORKSPACE_STATE_VARIANT[record.state]}
          testId={`setup-console-workspace-init-state-${record.workspaceId}`}
        >
          {t(WORKSPACE_STATE_LABEL_KEY[record.state])}
        </StateBadge>
      )
    },
    { title: t("setupConsoleWorkspaceColumnVersion"), dataIndex: "seedBundleVersion" },
    { title: t("setupConsoleWorkspaceColumnLastUpdated"), dataIndex: "lastUpdatedAt" },
    {
      title: "",
      dataIndex: "actions",
      render: (_value, workspace) => {
        const busy = busyWorkspaceId === workspace.workspaceId;
        const done = isWorkspaceInitDone(workspace.state);
        return (
          <Space>
            <Button
              type="primary"
              theme="solid"
              size="small"
              data-testid={`setup-console-workspace-init-run-${workspace.workspaceId}`}
              disabled={busy || done}
              loading={busy}
              onClick={() => void initialize(workspace)}
            >
              {done ? t("setupConsoleStepStateSucceeded") : t("setupConsoleSystemResume")}
            </Button>
            <Button
              type="tertiary"
              theme="light"
              size="small"
              data-testid={`setup-console-workspace-init-bundle-${workspace.workspaceId}`}
              disabled={busy}
              onClick={() => void applyBundle(workspace)}
            >
              v1
            </Button>
            <Button
              type="tertiary"
              theme="light"
              size="small"
              data-testid={`setup-console-workspace-init-complete-${workspace.workspaceId}`}
              disabled={busy || done}
              onClick={() => void complete(workspace)}
            >
              {t("setupConsoleStepComplete")}
            </Button>
          </Space>
        );
      }
    }
  ];

  return (
    <div data-testid="setup-console-workspace-init">
      {errorMessage ? (
        <div style={{ marginBottom: 12 }}>
          <InfoBanner
            variant="danger"
            title={t("setupConsoleStepStateFailed")}
            description={errorMessage}
            testId="setup-console-workspace-init-error"
          />
        </div>
      ) : null}

      {workspaces.length === 0 ? (
        <SectionCard>
          <Text type="tertiary">{t("setupConsoleWorkspaceEmpty")}</Text>
        </SectionCard>
      ) : (
        <SectionCard>
          <Table
            data-testid="setup-console-workspace-init-table"
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
                    "data-testid": `setup-console-workspace-init-row-${record.workspaceId}`
                  }
                : {}
            }
          />
        </SectionCard>
      )}
    </div>
  );
}

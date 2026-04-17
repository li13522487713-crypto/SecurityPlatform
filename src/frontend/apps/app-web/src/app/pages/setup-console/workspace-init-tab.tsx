import { useCallback, useState } from "react";
import { useAppI18n } from "../../i18n";
import { useBootstrap } from "../../bootstrap-context";
import {
  applyWorkspaceSeedBundle,
  completeWorkspaceInit,
  initializeWorkspace
} from "../../../services/mock";
import type { WorkspaceSetupStateDto } from "../../../services/api-setup-console";
import type { AppMessageKey } from "../../messages";
import { isWorkspaceInitDone, type WorkspaceSetupState } from "../../setup-console-state-machine";

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

const WORKSPACE_STATE_VARIANT: Record<WorkspaceSetupState, string> = {
  workspace_init_pending: "",
  workspace_init_running: "is-info",
  workspace_init_completed: "is-success",
  workspace_init_failed: "is-error"
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

  return (
    <div data-testid="setup-console-workspace-init">
      {errorMessage ? (
        <div className="atlas-warning-banner" data-testid="setup-console-workspace-init-error">
          <strong>{t("setupConsoleStepStateFailed")}</strong>
          <p>{errorMessage}</p>
        </div>
      ) : null}

      {workspaces.length === 0 ? (
        <section className="atlas-setup-panel">
          <p className="atlas-field-hint">{t("setupConsoleWorkspaceEmpty")}</p>
        </section>
      ) : (
        <table className="atlas-table" data-testid="setup-console-workspace-init-table">
          <thead>
            <tr>
              <th>{t("setupConsoleWorkspaceColumnId")}</th>
              <th>{t("setupConsoleWorkspaceColumnName")}</th>
              <th>{t("setupConsoleWorkspaceColumnState")}</th>
              <th>{t("setupConsoleWorkspaceColumnVersion")}</th>
              <th>{t("setupConsoleWorkspaceColumnLastUpdated")}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {workspaces.map((workspace) => {
              const busy = busyWorkspaceId === workspace.workspaceId;
              const done = isWorkspaceInitDone(workspace.state);
              return (
                <tr
                  key={workspace.workspaceId}
                  data-testid={`setup-console-workspace-init-row-${workspace.workspaceId}`}
                >
                  <td>{workspace.workspaceId}</td>
                  <td>{workspace.workspaceName}</td>
                  <td>
                    <span
                      className={`atlas-pill ${WORKSPACE_STATE_VARIANT[workspace.state]}`.trim()}
                      data-testid={`setup-console-workspace-init-state-${workspace.workspaceId}`}
                    >
                      {t(WORKSPACE_STATE_LABEL_KEY[workspace.state])}
                    </span>
                  </td>
                  <td>{workspace.seedBundleVersion}</td>
                  <td>{workspace.lastUpdatedAt}</td>
                  <td>
                    <div style={{ display: "flex", gap: 8 }}>
                      <button
                        type="button"
                        className="atlas-button atlas-button--primary"
                        data-testid={`setup-console-workspace-init-run-${workspace.workspaceId}`}
                        disabled={busy || done}
                        onClick={() => void initialize(workspace)}
                      >
                        {done
                          ? t("setupConsoleStepStateSucceeded")
                          : t("setupConsoleSystemResume")}
                      </button>
                      <button
                        type="button"
                        className="atlas-button atlas-button--secondary"
                        data-testid={`setup-console-workspace-init-bundle-${workspace.workspaceId}`}
                        disabled={busy}
                        onClick={() => void applyBundle(workspace)}
                      >
                        v1
                      </button>
                      <button
                        type="button"
                        className="atlas-button atlas-button--secondary"
                        data-testid={`setup-console-workspace-init-complete-${workspace.workspaceId}`}
                        disabled={busy || done}
                        onClick={() => void complete(workspace)}
                      >
                        {t("setupConsoleStepComplete")}
                      </button>
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </div>
  );
}

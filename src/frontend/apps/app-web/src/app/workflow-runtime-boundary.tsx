import type { ReactNode } from "react";
import { useAppI18n } from "./i18n";
import { useBootstrap } from "./bootstrap-context";
import { useAppStartup } from "./startup-kernel";
import { useOptionalWorkspaceContext } from "./workspace-context";
import { useOptionalOrganizationContext } from "./organization-context";

function WorkflowRuntimeStatusCard({
  title,
  description,
  actionLabel,
  onAction
}: {
  title: string;
  description: string;
  actionLabel?: string;
  onAction?: () => void | Promise<void>;
}) {
  return (
    <div className="atlas-centered-page">
      <div className="atlas-status-card atlas-status-card--workflow">
        <div className="atlas-status-card__body">
          <h1>{title}</h1>
          <p>{description}</p>
          {actionLabel && onAction ? (
            <div className="atlas-setup-actions">
              <button type="button" className="atlas-button atlas-button--primary" onClick={() => void onAction()}>
                {actionLabel}
              </button>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}

export function WorkflowRuntimeBoundary({ children }: { children: ReactNode }) {
  const { t } = useAppI18n();
  const bootstrap = useBootstrap();
  const startup = useAppStartup();
  const workspace = useOptionalWorkspaceContext();
  const organization = useOptionalOrganizationContext();

  if (bootstrap.loading || !startup.bootstrapReady || workspace?.loading) {
    return (
      <div className="atlas-loading-page" data-testid="workflow-runtime-loading">
        {t("workflowRuntimeLoading")}
      </div>
    );
  }

  if (!startup.platformReady) {
    return (
      <WorkflowRuntimeStatusCard
        title={t("platformNotReadyTitle")}
        description={t("platformNotReadyDesc")}
      />
    );
  }

  if (!startup.appReady) {
    return (
      <WorkflowRuntimeStatusCard
        title={t("appSetupTitle")}
        description={t("appSetupDesc")}
      />
    );
  }

  if (startup.featureFlagsLoading || !startup.featureFlagsReady) {
    return (
      <div className="atlas-loading-page" data-testid="workflow-runtime-preparing">
        {t("workflowRuntimePreparing")}
      </div>
    );
  }

  if (startup.featureFlagsError) {
    return (
      <WorkflowRuntimeStatusCard
        title={t("workflowRuntimeUnavailableTitle")}
        description={t("workflowRuntimeUnavailableDesc")}
        actionLabel={t("refresh")}
        onAction={startup.refreshFeatureFlags}
      />
    );
  }

  if (!startup.spaceReady || !workspace?.id || !workspace.appKey || !organization?.orgId) {
    return (
      <WorkflowRuntimeStatusCard
        title={t("workflowRuntimeUnavailableTitle")}
        description={t("workflowRuntimeWorkspaceDesc")}
      />
    );
  }

  return <>{children}</>;
}

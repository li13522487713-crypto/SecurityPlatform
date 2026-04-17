import { useEffect, useState, type ReactNode } from "react";
import { I18nProvider } from "../../../../packages/arch/i18n/src/i18n-provider";
import { I18n, initI18nInstance } from "../../../../packages/arch/i18n/src/raw";
import { useAppI18n } from "./i18n";
import type { AppLocale } from "./messages";
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

function toCozeLocale(locale: AppLocale): "en" | "zh-CN" {
  return locale === "en-US" ? "en" : "zh-CN";
}

export function WorkflowRuntimeBoundary({ children }: { children: ReactNode }) {
  const { locale, t } = useAppI18n();
  const bootstrap = useBootstrap();
  const startup = useAppStartup();
  const workspace = useOptionalWorkspaceContext();
  const organization = useOptionalOrganizationContext();
  const [cozeReady, setCozeReady] = useState(false);
  const cozeLocale = toCozeLocale(locale);

  useEffect(() => {
    let cancelled = false;
    setCozeReady(false);

    try {
      window.localStorage.setItem("i18next", cozeLocale);
    } catch {
      // 忽略本地存储异常，避免阻断编辑器加载。
    }

    Promise.race([
      initI18nInstance({ lng: cozeLocale }),
      new Promise(resolve => setTimeout(resolve, 500))
    ]).then(() => {
      I18n.setLang(cozeLocale);
      if (!cancelled) {
        setCozeReady(true);
      }
    }).catch(() => {
      I18n.setLang(cozeLocale);
      if (!cancelled) {
        setCozeReady(true);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [cozeLocale]);

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

  if (!cozeReady) {
    return (
      <div className="atlas-loading-page" data-testid="workflow-runtime-i18n-loading">
        {t("workflowRuntimePreparing")}
      </div>
    );
  }

  return <I18nProvider i18n={I18n}>{children}</I18nProvider>;
}

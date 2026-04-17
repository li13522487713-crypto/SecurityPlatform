import { useEffect, useState, type ReactNode } from "react";
import { I18nProvider } from "../../../../packages/arch/i18n/src/i18n-provider";
import { I18n, initI18nInstance } from "../../../../packages/arch/i18n/src/raw";
import { setAtlasFoundationHost } from "@atlas/foundation-bridge";
import { useAppI18n } from "./i18n";
import type { AppLocale } from "./messages";
import { useOptionalAuth } from "./auth-context";
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

/**
 * 把 Atlas 的 AppLocale 映射为 cozelib (i18next) 能识别的语言代码。
 *
 * 决策矩阵（与 cozelib 上游 i18n key 对齐）：
 * - "zh-CN" / "zh-cn" / "zh" / "zh-Hans" 等中文变体 → "zh-CN"
 * - 其他任意值（包括空、未知、"en-US" / "en-GB" 等英文变体） → "en"
 *
 * 暴露为 named export 以便 Vitest 单测对边界条件做覆盖（风险 4）。
 */
export function toCozeLocale(locale: AppLocale | string | null | undefined): "en" | "zh-CN" {
  if (typeof locale !== "string" || locale.trim() === "") {
    return "zh-CN";
  }
  const normalized = locale.trim().toLowerCase();
  if (normalized === "zh-cn" || normalized === "zh" || normalized.startsWith("zh-")) {
    return "zh-CN";
  }
  return "en";
}

export function WorkflowRuntimeBoundary({ children }: { children: ReactNode }) {
  const { locale, t } = useAppI18n();
  const bootstrap = useBootstrap();
  const startup = useAppStartup();
  const workspace = useOptionalWorkspaceContext();
  const organization = useOptionalOrganizationContext();
  // 单测场景下可能没有 AuthProvider，使用可选读取避免破坏 hooks 顺序。
  const auth = useOptionalAuth();
  const [cozeReady, setCozeReady] = useState(false);
  const cozeLocale = toCozeLocale(locale);

  // 在 cozelib 初始化前把 Atlas 用户 / 空间 / 主题 / 登录态注入桥接层，
  // 防止编辑器内任何 useUserInfo / useSpace 取到 null 造成整页崩溃。
  useEffect(() => {
    const profile = auth?.profile;
    const workspaceId = workspace?.id ?? bootstrap.spaceId;
    const workspaceName = workspace?.name ?? "Atlas Workspace";
    setAtlasFoundationHost({
      loginStatus: auth?.isAuthenticated ? "logined" : auth?.loading ? "settling" : "not_login",
      theme: "light",
      user: profile
        ? {
            userIdStr: profile.id,
            name: profile.displayName || profile.username,
            screenName: profile.username,
            email: undefined,
            avatarUrl: undefined,
            locale,
            tenantId: profile.tenantId,
          }
        : null,
      spaces: workspaceId
        ? [
            {
              id: workspaceId,
              name: workspaceName,
              description: workspace?.description,
              iconUrl: workspace?.icon,
              spaceType: 2,
              spaceMode: 0,
              roleType: workspace?.roleCode === "Owner" ? 1 : workspace?.roleCode === "Admin" ? 2 : 3,
            },
          ]
        : [],
      activeSpaceId: workspaceId,
    });
  }, [
    auth?.isAuthenticated,
    auth?.loading,
    auth?.profile,
    bootstrap.spaceId,
    locale,
    workspace?.id,
    workspace?.name,
    workspace?.description,
    workspace?.icon,
    workspace?.roleCode
  ]);

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

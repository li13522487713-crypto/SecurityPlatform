import { useEffect, useState, type CSSProperties, type ReactNode } from "react";
import { I18nProvider } from "../../../../packages/arch/i18n/src/i18n-provider";
import { I18n, initI18nInstance } from "../../../../packages/arch/i18n/src/raw";
import { useAppI18n } from "./i18n";
import type { AppLocale } from "./messages";
import { useBootstrap } from "./bootstrap-context";
import { useAppStartup } from "./startup-kernel";
import { useOptionalWorkspaceContext } from "./workspace-context";

/**
 * 注意：本文件刻意不引入 `@douyinfe/semi-ui` 组件。
 * - 工作流运行边界处于 React 渲染早期阶段，且作为 cozelib i18n 的容器，
 *   引入 Semi 会触发 lottie-web 的副作用 import，使 jsdom 单测崩溃。
 * - 这里的状态卡片只承担"加载中 / 失败"等极简文案展示，使用纯 inline style + 共享 token 即可。
 */
const STATUS_PAGE_STYLE: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  minHeight: "100vh",
  padding: 32,
  width: "100%"
};

const STATUS_CARD_STYLE: CSSProperties = {
  width: "min(520px, 100%)",
  padding: "32px 28px",
  borderRadius: 12,
  background: "var(--semi-color-bg-2, #ffffff)",
  border: "1px solid var(--semi-color-border, rgba(15,23,42,0.08))",
  boxShadow: "0 8px 24px rgba(15,23,42,0.06)"
};

const STATUS_TITLE_STYLE: CSSProperties = {
  margin: 0,
  fontSize: 18,
  fontWeight: 600,
  color: "var(--semi-color-text-0, #0f172a)"
};

const STATUS_DESC_STYLE: CSSProperties = {
  margin: "8px 0 0",
  color: "var(--semi-color-text-2, #64748b)"
};

const STATUS_ACTION_BTN: CSSProperties = {
  marginTop: 16,
  padding: "8px 16px",
  borderRadius: 8,
  border: "none",
  background: "var(--semi-color-primary, #1677ff)",
  color: "#fff",
  cursor: "pointer",
  fontSize: 14
};

const LOADING_PAGE_STYLE: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  minHeight: "100vh",
  padding: 32,
  color: "var(--semi-color-text-2, #64748b)"
};

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
    <div style={STATUS_PAGE_STYLE}>
      <div style={STATUS_CARD_STYLE}>
        <h1 style={STATUS_TITLE_STYLE}>{title}</h1>
        <p style={STATUS_DESC_STYLE}>{description}</p>
        {actionLabel && onAction ? (
          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <button type="button" style={STATUS_ACTION_BTN} onClick={() => void onAction()}>
              {actionLabel}
            </button>
          </div>
        ) : null}
      </div>
    </div>
  );
}

function WorkflowRuntimeLoadingPage({ tip, testId }: { tip: ReactNode; testId: string }) {
  return (
    <div style={LOADING_PAGE_STYLE} data-testid={testId}>
      {tip}
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

export function WorkflowRuntimeBoundary({
  children,
  spaceId
}: {
  children: ReactNode;
  spaceId?: string;
}) {
  const { locale, t } = useAppI18n();
  const bootstrap = useBootstrap();
  const startup = useAppStartup();
  const workspace = useOptionalWorkspaceContext();
  const [cozeReady, setCozeReady] = useState(false);
  const cozeLocale = toCozeLocale(locale);
  const resolvedSpaceId = typeof spaceId === "string" && spaceId.trim().length > 0
    ? spaceId.trim()
    : (workspace?.id ?? "");

  useEffect(() => {
    if (cozeReady) {
      I18n.setLang(cozeLocale);
      return;
    }

    let cancelled = false;
    initI18nInstance({ language: cozeLocale })
      .then(() => {
        I18n.setLang(cozeLocale);
        if (!cancelled) {
          setCozeReady(true);
        }
      })
      .catch(() => {
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
      <WorkflowRuntimeLoadingPage
        tip={t("workflowRuntimeLoading")}
        testId="workflow-runtime-loading"
      />
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
      <WorkflowRuntimeLoadingPage
        tip={t("workflowRuntimePreparing")}
        testId="workflow-runtime-preparing"
      />
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

  if (!startup.spaceReady || !resolvedSpaceId) {
    return (
      <WorkflowRuntimeStatusCard
        title={t("workflowRuntimeUnavailableTitle")}
        description={t("workflowRuntimeWorkspaceDesc")}
      />
    );
  }

  if (!cozeReady) {
    return (
      <WorkflowRuntimeLoadingPage
        tip={t("workflowRuntimePreparing")}
        testId="workflow-runtime-i18n-loading"
      />
    );
  }

  return <I18nProvider i18n={I18n}>{children}</I18nProvider>;
}

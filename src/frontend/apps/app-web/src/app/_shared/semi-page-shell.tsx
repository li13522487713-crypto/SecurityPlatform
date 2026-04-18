import type { CSSProperties, ReactNode } from "react";
import { Spin } from "@douyinfe/semi-ui";

/**
 * 应用全局页面壳：替换历史 `atlas-loading-page` / `atlas-centered-page` 容器。
 *
 * 设计取舍：
 * - `loading=true` 时只渲染 Semi `Spin`，不暴露内部 children（避免 loading 与内容重叠闪烁）。
 * - `centered=true` 时 children 居中并加最大宽度容器（默认 720px）；否则只是页面级 padding 容器，
 *   交由 children 自行布局，便于宽屏控制台/表单/向导一致接入。
 * - 不引入新 CSS：尺寸/留白只用 inline style + Semi 的 token，避免与 app.css 内残留 `.atlas-*`
 *   选择器互相冲突，方便 M7 一次性清掉旧规则。
 */
export interface PageShellProps {
  /** 是否处于加载态：只渲染居中 Spin，忽略 children */
  loading?: boolean;
  /** 加载提示文案，仅 `loading=true` 时显示 */
  loadingTip?: ReactNode;
  /** Spin 尺寸，默认 large */
  loadingSize?: "small" | "middle" | "large";
  /** 是否水平/垂直居中并约束最大宽度（适用于登录、向导等单卡片页面） */
  centered?: boolean;
  /** 居中模式下卡片最大宽度，默认 720px */
  maxWidth?: number | string;
  /** 透传 data-testid（兼容现有 e2e 选择器，例如 setup/login 页面外壳） */
  testId?: string;
  /** 自定义最外层 className（仅作为 hook，主体样式由 Semi 控制） */
  className?: string;
  /** 自定义 style，会与默认 padding/居中样式合并 */
  style?: CSSProperties;
  children?: ReactNode;
}

const BASE_STYLE: CSSProperties = {
  minHeight: "100vh",
  width: "100%",
  display: "flex",
  flexDirection: "column",
  boxSizing: "border-box"
};

const CENTERED_OUTER_STYLE: CSSProperties = {
  ...BASE_STYLE,
  alignItems: "center",
  justifyContent: "center",
  padding: "32px 16px"
};

const PLAIN_OUTER_STYLE: CSSProperties = {
  ...BASE_STYLE,
  padding: 0
};

const LOADING_OUTER_STYLE: CSSProperties = {
  ...BASE_STYLE,
  alignItems: "center",
  justifyContent: "center",
  padding: 32,
  gap: 12
};

export function PageShell({
  loading = false,
  loadingTip,
  loadingSize = "large",
  centered = false,
  maxWidth = 720,
  testId,
  className,
  style,
  children
}: PageShellProps) {
  if (loading) {
    return (
      <div
        className={className}
        style={{ ...LOADING_OUTER_STYLE, ...style }}
        data-testid={testId}
      >
        <Spin size={loadingSize} />
        {loadingTip ? <span style={{ color: "var(--semi-color-text-2)" }}>{loadingTip}</span> : null}
      </div>
    );
  }

  if (centered) {
    return (
      <div
        className={className}
        style={{ ...CENTERED_OUTER_STYLE, ...style }}
        data-testid={testId}
      >
        <div style={{ width: "100%", maxWidth }}>{children}</div>
      </div>
    );
  }

  return (
    <div
      className={className}
      style={{ ...PLAIN_OUTER_STYLE, ...style }}
      data-testid={testId}
    >
      {children}
    </div>
  );
}

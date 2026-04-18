import type { CSSProperties, ReactNode } from "react";
import { Banner } from "@douyinfe/semi-ui";

/**
 * 信息条幅：替换历史 `atlas-info-banner` / `atlas-warning-banner`。
 *
 * 是 Semi `Banner` 的薄封装，提供：
 * - 统一 `variant` 命名（`info` / `warning` / `danger`），更贴近内部约定；
 * - 默认 `closeIcon={null}` + `bordered` 关闭，保持与登录/向导/控制台一致；
 * - `compact` 模式（无图标 + 收紧 padding），用于嵌在向导内部的轻量提示。
 */
export type InfoBannerVariant = "info" | "warning" | "danger" | "success";

export interface InfoBannerProps {
  variant?: InfoBannerVariant;
  title?: ReactNode;
  description?: ReactNode;
  /** 紧凑模式：无图标、padding 较小，适合嵌入表单/向导内部 */
  compact?: boolean;
  /** 是否允许关闭，默认 false（向导/装机场景大多需常驻） */
  closable?: boolean;
  testId?: string;
  className?: string;
  style?: CSSProperties;
  children?: ReactNode;
}

export function InfoBanner({
  variant = "info",
  title,
  description,
  compact = false,
  closable = false,
  testId,
  className,
  style,
  children
}: InfoBannerProps) {
  const desc = description ?? children;
  return (
    <Banner
      type={variant}
      title={title}
      description={desc}
      closeIcon={closable ? undefined : null}
      fullMode={false}
      bordered={!compact}
      className={className}
      style={style}
      data-testid={testId}
    />
  );
}

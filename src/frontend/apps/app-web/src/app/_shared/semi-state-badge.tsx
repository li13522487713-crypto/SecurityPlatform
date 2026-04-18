import type { CSSProperties, ReactNode } from "react";
import { Tag } from "@douyinfe/semi-ui";

/**
 * 状态徽标：替换历史 `atlas-pill is-success | is-info | is-error | is-warning`。
 *
 * 直接基于 Semi `Tag` 暴露固定语义色板：
 * - success → 绿色（连接成功、初始化完成）
 * - info → 蓝色（运行中、进行中）
 * - warning → 黄色（待处理、待审核）
 * - danger → 红色（失败、错误）
 * - neutral → 灰色（未启动、已忽略）
 */
export type StateBadgeVariant = "success" | "info" | "warning" | "danger" | "neutral";

export interface StateBadgeProps {
  variant?: StateBadgeVariant;
  testId?: string;
  className?: string;
  style?: CSSProperties;
  children?: ReactNode;
}

const VARIANT_TO_SEMI: Record<
  StateBadgeVariant,
  { color: "green" | "blue" | "amber" | "red" | "grey" }
> = {
  success: { color: "green" },
  info: { color: "blue" },
  warning: { color: "amber" },
  danger: { color: "red" },
  neutral: { color: "grey" }
};

export function StateBadge({
  variant = "neutral",
  testId,
  className,
  style,
  children
}: StateBadgeProps) {
  const tagProps = VARIANT_TO_SEMI[variant];
  return (
    <Tag
      type="light"
      color={tagProps.color}
      className={className}
      style={style}
      data-testid={testId}
    >
      {children}
    </Tag>
  );
}

import type { CSSProperties, ReactNode } from "react";
import { Card, Typography } from "@douyinfe/semi-ui";

const { Title, Text } = Typography;

/**
 * 表单/向导主卡片：替换历史 `atlas-login-card` / `atlas-setup-card` / `atlas-hero-card`。
 *
 * - 围绕 Semi `Card` 包一层标题/副标题/操作区，避免业务页面到处自己拼 `<div className="atlas-...">`。
 * - 不强制设置卡片宽度：宽度由父 `PageShell.centered` + `maxWidth` 控制；本组件只负责"内容容器"语义。
 * - actions 渲染在标题下方副标题之后，留出 16px 间距；如果 actions 为 null，整块布局会自动收紧。
 * - 暴露 `headerExtra` 用于在右上角放语言切换/帮助按钮等"非主操作"控件。
 */
export interface FormCardProps {
  title?: ReactNode;
  subtitle?: ReactNode;
  /** 卡片右上角操作区（语言切换、退出按钮等非主操作） */
  headerExtra?: ReactNode;
  /** 卡片底部操作区（提交按钮、上一步/下一步等主操作） */
  actions?: ReactNode;
  /** 透传 data-testid */
  testId?: string;
  className?: string;
  style?: CSSProperties;
  /** 卡片 body padding，默认 24px */
  bodyPadding?: number | string;
  children?: ReactNode;
}

export function FormCard({
  title,
  subtitle,
  headerExtra,
  actions,
  testId,
  className,
  style,
  bodyPadding = 24,
  children
}: FormCardProps) {
  const headerNode =
    title || subtitle || headerExtra ? (
      <div
        style={{
          display: "flex",
          alignItems: "flex-start",
          justifyContent: "space-between",
          gap: 16,
          marginBottom: 16
        }}
      >
        <div style={{ flex: 1, minWidth: 0 }}>
          {title ? (
            <Title heading={3} style={{ margin: 0 }}>
              {title}
            </Title>
          ) : null}
          {subtitle ? (
            <Text type="tertiary" style={{ display: "block", marginTop: 6 }}>
              {subtitle}
            </Text>
          ) : null}
        </div>
        {headerExtra ? <div>{headerExtra}</div> : null}
      </div>
    ) : null;

  return (
    <Card
      className={className}
      style={style}
      bodyStyle={{ padding: bodyPadding }}
      data-testid={testId}
    >
      {headerNode}
      <div>{children}</div>
      {actions ? (
        <div
          style={{
            marginTop: 24,
            display: "flex",
            alignItems: "center",
            justifyContent: "flex-end",
            gap: 12
          }}
        >
          {actions}
        </div>
      ) : null}
    </Card>
  );
}

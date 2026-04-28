import type { CSSProperties, ReactNode } from "react";
import { Card, Typography } from "@douyinfe/semi-ui";

const { Title, Text } = Typography;

/**
 * 内容分区卡片：替换历史 `atlas-setup-panel` / `atlas-org-section`。
 *
 * 与 `FormCard` 的区别：
 * - `FormCard` 是"页面级容器"（一页通常只有一个，带页面标题）；
 * - `SectionCard` 是"页面内的多个区块"（设置控制台 dashboard 4 张卡、向导 step 内的 panel 等）。
 *
 * 视觉特征：
 * - 标题用 `Typography.Title heading={5}`，比 FormCard 小一档；
 * - 右上角放 actions（按钮组）；底部不强制 actions 区域，由 children 自由组合。
 */
export interface SectionCardProps {
  title?: ReactNode;
  subtitle?: ReactNode;
  /** 卡片标题右侧操作按钮 */
  actions?: ReactNode;
  testId?: string;
  className?: string;
  style?: CSSProperties;
  bodyPadding?: number | string;
  /** 卡片之间的下边距，默认 16px */
  marginBottom?: number | string;
  children?: ReactNode;
}

export function SectionCard({
  title,
  subtitle,
  actions,
  testId,
  className,
  style,
  bodyPadding = 20,
  marginBottom = 16,
  children
}: SectionCardProps) {
  const headerNode =
    title || subtitle || actions ? (
      <div
        style={{
          display: "flex",
          alignItems: "flex-start",
          justifyContent: "space-between",
          gap: 12,
          marginBottom: 16
        }}
      >
        <div style={{ flex: 1, minWidth: 0 }}>
          {title ? (
            <Title heading={5} style={{ margin: 0 }}>
              {title}
            </Title>
          ) : null}
          {subtitle ? (
            <Text type="tertiary" style={{ display: "block", marginTop: 4, fontSize: 12 }}>
              {subtitle}
            </Text>
          ) : null}
        </div>
        {actions ? <div style={{ display: "flex", gap: 8, flexShrink: 0 }}>{actions}</div> : null}
      </div>
    ) : null;

  return (
    <Card
      className={className}
      style={{ marginBottom, ...style }}
      bodyStyle={{ padding: bodyPadding }}
      data-testid={testId}
    >
      {headerNode}
      <div>{children}</div>
    </Card>
  );
}

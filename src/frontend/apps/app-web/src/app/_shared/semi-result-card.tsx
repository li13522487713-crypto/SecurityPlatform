import type { CSSProperties, ReactNode } from "react";
import { Card, Typography } from "@douyinfe/semi-ui";
import {
  IconTickCircle,
  IconAlertCircle,
  IconClose,
  IconInfoCircle
} from "@douyinfe/semi-icons";

const { Title, Text } = Typography;

/**
 * 结果卡片：替换历史 `atlas-result-card` / `atlas-not-ready-result`。
 *
 * 之所以不直接用 Semi 的 `Empty`：
 * - `Empty` 主打"无数据/空状态"，没有 status 语义；
 * - 引导/装机流程的成功/失败/警告卡片需要明确语义图标和强调主题色；
 * - 自封装一层后，统一颜色规范并保持与 `atlas-result-card--success/error` 行为一致。
 *
 * 不使用 Semi `Result` 组件：当前 Semi 版本未对外稳定导出 Result，避免引入构建期不确定性。
 */
export type ResultCardStatus = "success" | "warning" | "error" | "info";

export interface ResultCardProps {
  status?: ResultCardStatus;
  title?: ReactNode;
  description?: ReactNode;
  /** 底部操作按钮区域 */
  actions?: ReactNode;
  /** 额外内容（用于 setup 完成卡片放 Descriptions/banner 等） */
  extra?: ReactNode;
  testId?: string;
  className?: string;
  style?: CSSProperties;
}

const STATUS_CONFIG: Record<
  ResultCardStatus,
  { color: string; renderIcon: (color: string) => ReactNode }
> = {
  success: {
    color: "var(--semi-color-success)",
    renderIcon: (color) => <IconTickCircle size="extra-large" style={{ color }} />
  },
  warning: {
    color: "var(--semi-color-warning)",
    renderIcon: (color) => <IconAlertCircle size="extra-large" style={{ color }} />
  },
  error: {
    color: "var(--semi-color-danger)",
    renderIcon: (color) => <IconClose size="extra-large" style={{ color }} />
  },
  info: {
    color: "var(--semi-color-info)",
    renderIcon: (color) => <IconInfoCircle size="extra-large" style={{ color }} />
  }
};

export function ResultCard({
  status = "info",
  title,
  description,
  actions,
  extra,
  testId,
  className,
  style
}: ResultCardProps) {
  const config = STATUS_CONFIG[status];
  return (
    <Card
      className={className}
      style={style}
      bodyStyle={{ padding: "32px 24px" }}
      data-testid={testId}
    >
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          gap: 12,
          textAlign: "center"
        }}
      >
        <div style={{ marginBottom: 4 }}>{config.renderIcon(config.color)}</div>
        {title ? (
          <Title heading={4} style={{ margin: 0 }}>
            {title}
          </Title>
        ) : null}
        {description ? (
          <Text type="tertiary" style={{ maxWidth: 480 }}>
            {description}
          </Text>
        ) : null}
        {extra ? <div style={{ width: "100%", marginTop: 16 }}>{extra}</div> : null}
        {actions ? (
          <div
            style={{
              marginTop: 16,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              gap: 12,
              flexWrap: "wrap"
            }}
          >
            {actions}
          </div>
        ) : null}
      </div>
    </Card>
  );
}

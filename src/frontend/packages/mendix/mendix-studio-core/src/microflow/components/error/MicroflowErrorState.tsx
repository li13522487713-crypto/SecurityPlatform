import { Button, Empty, Space, Tag, Typography } from "@douyinfe/semi-ui";

import { getMicroflowApiError, getMicroflowErrorActionHint, getMicroflowErrorUserMessage } from "../../adapter/http/microflow-api-error";

const { Text } = Typography;

export interface MicroflowErrorStateProps {
  error: unknown;
  title?: string;
  compact?: boolean;
  onRetry?: () => void;
  onBack?: () => void;
}

export function MicroflowErrorState({ error, title, compact, onRetry, onBack }: MicroflowErrorStateProps) {
  const apiError = getMicroflowApiError(error);
  const description = getMicroflowErrorUserMessage(apiError);
  const hint = getMicroflowErrorActionHint(apiError);
  return (
    <Empty
      title={title ?? "微流服务异常"}
      description={(
        <Space vertical spacing={6} align="center">
          <Text>{description}</Text>
          {apiError.message && apiError.message !== description ? <Text type="tertiary" size="small">{apiError.message}</Text> : null}
          {hint ? <Text type="tertiary" size="small">{hint}</Text> : null}
          <Space wrap>
            <Tag color="red">{apiError.code}</Tag>
            {apiError.httpStatus ? <Tag>HTTP {apiError.httpStatus}</Tag> : null}
            {apiError.traceId ? <Tag>Trace {apiError.traceId}</Tag> : null}
          </Space>
        </Space>
      )}
      style={{ padding: compact ? 24 : 64 }}
    >
      <Space>
        {onRetry ? <Button onClick={onRetry}>重试</Button> : null}
        {onBack ? <Button onClick={onBack}>返回</Button> : null}
      </Space>
    </Empty>
  );
}

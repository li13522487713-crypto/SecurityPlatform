import { Button, Space, Tag, Typography } from "@douyinfe/semi-ui";

import type { MicroflowValidationSummary } from "../versions/microflow-version-types";

const { Text } = Typography;

export function PublishValidationSummary({ summary, onViewProblems }: { summary: MicroflowValidationSummary; onViewProblems?: () => void }) {
  return (
    <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
      <Text strong>发布前校验</Text>
      <Space wrap>
        <Tag color={summary.errorCount > 0 ? "red" : "green"}>错误 {summary.errorCount}</Tag>
        <Tag color={summary.warningCount > 0 ? "orange" : "grey"}>警告 {summary.warningCount}</Tag>
        <Tag color="blue">提示 {summary.infoCount}</Tag>
        {onViewProblems ? <Button size="small" onClick={onViewProblems}>查看问题</Button> : null}
      </Space>
      {summary.errorCount > 0 ? <Text type="danger">存在错误，无法发布。</Text> : null}
      {summary.errorCount === 0 && summary.warningCount > 0 ? <Text type="warning">存在警告，可发布但建议先确认。</Text> : null}
    </Space>
  );
}

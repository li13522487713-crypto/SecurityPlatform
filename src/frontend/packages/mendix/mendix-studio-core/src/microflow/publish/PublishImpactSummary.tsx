import { Space, Tag, Typography } from "@douyinfe/semi-ui";

import { getImpactLevelColor, getImpactLevelLabel } from "../references/microflow-reference-utils";
import type { MicroflowPublishImpactAnalysis } from "./microflow-publish-types";

const { Text } = Typography;

export function PublishImpactSummary({ impact }: { impact?: MicroflowPublishImpactAnalysis }) {
  if (!impact) {
    return <Text type="tertiary">正在分析发布影响...</Text>;
  }
  return (
    <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
      <Text strong>发布影响分析</Text>
      <Space wrap>
        <Tag color={getImpactLevelColor(impact.impactLevel)}>影响级别 {getImpactLevelLabel(impact.impactLevel)}</Tag>
        <Tag color="blue">引用 {impact.summary.referenceCount}</Tag>
        <Tag color={impact.summary.breakingChangeCount > 0 ? "orange" : "green"}>破坏性变更 {impact.summary.breakingChangeCount}</Tag>
        <Tag color="red">高 {impact.summary.highImpactCount}</Tag>
        <Tag color="orange">中 {impact.summary.mediumImpactCount}</Tag>
        <Tag color="blue">低 {impact.summary.lowImpactCount}</Tag>
      </Space>
    </Space>
  );
}

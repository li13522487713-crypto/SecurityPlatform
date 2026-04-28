import { Collapse, Empty, Space, Tag, Typography } from "@douyinfe/semi-ui";
import type { FlowExecutionTraceSchema } from "@atlas/mendix-schema";

const { Text } = Typography;

export function DebugTracePanel({ trace }: { trace?: FlowExecutionTraceSchema }) {
  if (!trace) {
    return <Empty title="暂无执行链路" description="触发一次 Runtime Action 后将显示 Trace。" />;
  }

  return (
    <Space vertical style={{ width: "100%" }} spacing={12}>
      <Space wrap>
        <Tag color="blue">traceId: {trace.traceId}</Tag>
        <Tag>{trace.flowType}</Tag>
        <Tag>{trace.flowId}</Tag>
        <Tag color={trace.status === "failed" ? "red" : "green"}>{trace.status}</Tag>
      </Space>
      <Text type="tertiary">
        {trace.startedAt} - {trace.endedAt ?? "-"}
      </Text>
      <Collapse accordion>
        {trace.steps.map(step => (
          <Collapse.Panel
            key={step.stepId}
            itemKey={step.stepId}
            header={`${step.nodeType} (${step.nodeId})`}
          >
            <Space vertical style={{ width: "100%" }}>
              <Text strong>expressionResults</Text>
              <pre style={{ margin: 0, whiteSpace: "pre-wrap" }}>
                {JSON.stringify(step.expressionResults, null, 2)}
              </pre>
              <Text strong>permissionChecks</Text>
              <pre style={{ margin: 0, whiteSpace: "pre-wrap" }}>
                {JSON.stringify(step.permissionChecks, null, 2)}
              </pre>
              <Text strong>databaseQueries</Text>
              <pre style={{ margin: 0, whiteSpace: "pre-wrap" }}>
                {JSON.stringify(step.databaseQueries, null, 2)}
              </pre>
              <Text strong>uiCommands</Text>
              <pre style={{ margin: 0, whiteSpace: "pre-wrap" }}>
                {JSON.stringify(step.uiCommands, null, 2)}
              </pre>
              <Text strong>inputSnapshot</Text>
              <pre style={{ margin: 0, whiteSpace: "pre-wrap" }}>
                {JSON.stringify(step.inputSnapshot ?? {}, null, 2)}
              </pre>
              <Text strong>outputSnapshot</Text>
              <pre style={{ margin: 0, whiteSpace: "pre-wrap" }}>
                {JSON.stringify(step.outputSnapshot ?? {}, null, 2)}
              </pre>
            </Space>
          </Collapse.Panel>
        ))}
      </Collapse>
    </Space>
  );
}

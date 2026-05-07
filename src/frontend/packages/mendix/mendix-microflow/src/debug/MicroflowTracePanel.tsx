import { Button, Card, Empty, Space, Tabs, Tag, Typography } from "@douyinfe/semi-ui";

import { extractGatewayBranchTrace, type MicroflowRunSession, type MicroflowRuntimeError, type MicroflowTraceFrame } from "./trace-types";
import { buildExecutionPath } from "./trace-history-utils";
import { buildMicroflowNodeIoViewModel } from "./node-io-view-model";

const { Text } = Typography;

export interface MicroflowTracePanelProps {
  microflowId: string;
  microflowName?: string;
  session?: MicroflowRunSession;
  activeFrameId?: string;
  onSelectFrame: (frame: MicroflowTraceFrame) => void;
  onSelectFlow: (flowId: string) => void;
  onSelectError?: (error: MicroflowRuntimeError) => void;
}

function statusColor(status: string): "green" | "red" | "blue" | "grey" | "orange" {
  if (status === "success") {
    return "green";
  }
  if (status === "failed") {
    return "red";
  }
  if (status === "running") {
    return "blue";
  }
  if (status === "unsupported") {
    return "orange";
  }
  return "grey";
}

function collectErrors(session?: MicroflowRunSession): MicroflowRuntimeError[] {
  if (!session) {
    return [];
  }
  const errors: MicroflowRuntimeError[] = [];
  const walk = (current: MicroflowRunSession) => {
    if (current.error) {
      errors.push(current.error);
    }
    current.trace.forEach(frame => {
      if (frame.error) {
        errors.push(frame.error);
      }
    });
    current.childRuns?.forEach(walk);
  };
  walk(session);
  return errors;
}

function JsonBlock({ value }: { value: unknown }) {
  return <pre style={{ whiteSpace: "pre-wrap", margin: 0 }}>{JSON.stringify(value ?? null, null, 2)}</pre>;
}

function summarizeFramePayload(label: string, payload: unknown): string | null {
  if (payload == null) {
    return null;
  }
  if (Array.isArray(payload)) {
    return `${label}[${payload.length}]`;
  }
  if (typeof payload === "object") {
    const keys = Object.keys(payload as Record<string, unknown>);
    if (keys.length === 0) {
      return `${label}{}`;
    }
    const preview = keys.slice(0, 3).join(", ");
    const suffix = keys.length > 3 ? ` +${keys.length - 3}` : "";
    return `${label}{${preview}${suffix}}`;
  }
  return `${label}:${String(payload)}`;
}

function branchStatusColor(status: string): "green" | "red" | "blue" | "grey" {
  if (status === "completed") {
    return "green";
  }
  if (status === "failed") {
    return "red";
  }
  if (status === "executed") {
    return "blue";
  }
  return "grey";
}

export function MicroflowTracePanel({
  microflowId,
  microflowName,
  session,
  activeFrameId,
  onSelectFrame,
  onSelectFlow,
  onSelectError,
}: MicroflowTracePanelProps) {
  if (!session) {
    return <Empty title="No trace" description="Run this microflow or select a run history item." />;
  }
  const executionPath = buildExecutionPath(session);
  const activeFrame = activeFrameId ? executionPath.find(item => item.frame.id === activeFrameId)?.frame : executionPath[0]?.frame;
  const errors = collectErrors(session);
  const activeNodeIo = activeFrame ? buildMicroflowNodeIoViewModel(activeFrame) : undefined;
  const activeBranchTrace = activeNodeIo?.flow.branchTrace ?? [];

  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Card style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
        <Space wrap>
          <Tag color={statusColor(session.status)}>{session.status}</Tag>
          <Tag>runId {session.id}</Tag>
          <Tag>{microflowName || microflowId}</Tag>
          <Tag>{session.startedAt}</Tag>
          {session.endedAt ? <Tag>{Math.max(0, new Date(session.endedAt).getTime() - new Date(session.startedAt).getTime())}ms</Tag> : null}
          {session.error?.message ? <Tag color="red">{session.error.message}</Tag> : null}
        </Space>
      </Card>
      <Tabs type="line" style={{ width: "100%" }}>
        <Tabs.TabPane tab="Execution Path" itemKey="execution-path">
          <Space vertical align="start" style={{ width: "100%" }}>
            {executionPath.length === 0 ? <Empty title="No executed nodes" /> : executionPath.map((item, index) => (
              <Card
                key={`${item.frame.id}:${index}`}
                style={{
                  width: "100%",
                  borderColor: item.frame.id === activeFrameId ? "var(--semi-color-primary)" : undefined,
                  opacity: item.frame.status === "skipped" ? 0.65 : 1,
                }}
                bodyStyle={{ padding: 10, paddingLeft: 10 + item.callDepth * 16 }}
              >
                <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
                  <button
                    type="button"
                    style={{ border: "none", background: "transparent", textAlign: "left", cursor: "pointer", flex: 1, padding: 0 }}
                    onClick={() => onSelectFrame(item.frame)}
                  >
                    <Text strong>{index + 1}. {item.frame.nodeTitle || item.frame.objectTitle || item.frame.objectId}</Text>
                    <br />
                    <Text size="small" type="tertiary">
                      {item.frame.nodeKind || item.frame.nodeType || item.frame.actionKind || item.frame.actionId || "node"} · {item.frame.objectId} · {item.frame.durationMs}ms · depth {item.callDepth}
                    </Text>
                    {extractGatewayBranchTrace(item.frame).length ? (
                      <>
                        <br />
                        <Text size="small" type="tertiary">
                          branches {extractGatewayBranchTrace(item.frame).filter(branch => branch.selected).length}/{extractGatewayBranchTrace(item.frame).length}
                        </Text>
                      </>
                    ) : null}
                    {(() => {
                      const inputSummary = summarizeFramePayload("in", item.frame.input);
                      const outputSummary = summarizeFramePayload("out", item.frame.output);
                      if (!inputSummary && !outputSummary) {
                        return null;
                      }
                      return (
                        <>
                          <br />
                          <Text size="small" type="tertiary">
                            {[inputSummary, outputSummary].filter(Boolean).join(" · ")}
                          </Text>
                        </>
                      );
                    })()}
                    {item.frame.error ? (
                      <>
                        <br />
                        <Text size="small" type="danger">
                          {item.frame.error.code}: {item.frame.error.message}
                        </Text>
                      </>
                    ) : null}
                  </button>
                  <Space>
                    {item.frame.incomingFlowId ? <Tag onClick={() => onSelectFlow(item.frame.incomingFlowId as string)}>in</Tag> : null}
                    {item.frame.outgoingFlowId ? <Tag onClick={() => onSelectFlow(item.frame.outgoingFlowId as string)}>out</Tag> : null}
                    <Tag color={statusColor(item.frame.error?.code?.includes("UNSUPPORTED") ? "unsupported" : item.frame.status)}>{item.frame.status}</Tag>
                  </Space>
                </Space>
              </Card>
            ))}
          </Space>
        </Tabs.TabPane>
        <Tabs.TabPane tab="Node Results" itemKey="node-results">
          <pre style={{ whiteSpace: "pre-wrap", margin: 0 }}>{JSON.stringify(executionPath.map(item => ({
            nodeId: item.frame.nodeId ?? item.frame.objectId,
            nodeName: item.frame.nodeTitle ?? item.frame.objectTitle,
            nodeType: item.frame.nodeKind ?? item.frame.nodeType ?? item.frame.actionKind ?? item.frame.actionId,
            microflowId: item.frame.microflowId ?? microflowId,
            status: item.frame.status,
            inputSnapshot: item.frame.input,
            actionInput: item.frame.actionInput,
            inputVariables: item.frame.inputVariables,
            outputSnapshot: item.frame.output,
            outputVariables: item.frame.outputVariables,
            variableDelta: item.frame.variableDelta,
            branchTrace: extractGatewayBranchTrace(item.frame),
            selectedCaseValue: item.frame.selectedCaseValue,
            handoffPayload: item.frame.handoffPayload,
            transactionEffect: item.frame.transactionEffect,
            errorMessage: item.frame.error?.message,
            durationMs: item.frame.durationMs,
            callDepth: item.callDepth,
            callPath: item.microflowPath,
            parentNodeId: item.frame.callerObjectId,
          })), null, 2)}</pre>
        </Tabs.TabPane>
        <Tabs.TabPane tab="Node I/O" itemKey="node-io">
          {!activeFrame ? <Empty title="No active frame" /> : (
            <Space vertical align="start" style={{ width: "100%" }}>
              <Card style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
                <Space wrap>
                  <Tag>{activeFrame.objectId}</Tag>
                  <Tag>{activeNodeIo?.summary.nodeKind}</Tag>
                  {activeNodeIo?.summary.actionKind ? <Tag>{activeNodeIo.summary.actionKind}</Tag> : null}
                  <Tag color={statusColor(activeFrame.status)}>{activeNodeIo?.summary.status}</Tag>
                  <Tag>{activeNodeIo?.summary.durationMs}ms</Tag>
                </Space>
              </Card>
              <Tabs type="card" style={{ width: "100%" }}>
                <Tabs.TabPane tab="Input" itemKey="input">
                  <JsonBlock value={activeNodeIo?.input} />
                </Tabs.TabPane>
                <Tabs.TabPane tab="Output" itemKey="output">
                  <JsonBlock value={activeNodeIo?.output} />
                </Tabs.TabPane>
                <Tabs.TabPane tab="Flow" itemKey="flow">
                  <JsonBlock value={activeNodeIo?.flow} />
                </Tabs.TabPane>
                {activeBranchTrace.length ? (
                  <Tabs.TabPane tab="Branches" itemKey="branches">
                    <Space vertical align="start" style={{ width: "100%" }}>
                      {activeBranchTrace.map(branch => (
                        <Card key={`${branch.flowId}:${branch.branchId}`} style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
                          <Space wrap>
                            <Tag color={branchStatusColor(branch.status)}>{branch.status}</Tag>
                            <Tag>{branch.selected ? "selected" : "skipped"}</Tag>
                            <Tag onClick={() => onSelectFlow(branch.flowId)}>flow {branch.flowId}</Tag>
                            <Tag>branch {branch.branchId}</Tag>
                            {branch.targetObjectId ? <Tag>target {branch.targetObjectId}</Tag> : null}
                          </Space>
                        </Card>
                      ))}
                    </Space>
                  </Tabs.TabPane>
                ) : null}
                <Tabs.TabPane tab="Runtime" itemKey="runtime">
                  <JsonBlock value={activeNodeIo?.runtime} />
                </Tabs.TabPane>
              </Tabs>
            </Space>
          )}
        </Tabs.TabPane>
        <Tabs.TabPane tab="Call Stack" itemKey="call-stack">
          <Space vertical align="start" style={{ width: "100%" }}>
            {(session.callStack?.length ?? 0) === 0 ? <Empty title="No call stack" /> : (
              <Text>{session.callStack?.join(" -> ")}</Text>
            )}
            {session.childRuns?.length ? <Text type="tertiary">child runs: {session.childRuns.length}</Text> : null}
          </Space>
        </Tabs.TabPane>
        <Tabs.TabPane tab="Inputs" itemKey="inputs">
          <pre style={{ whiteSpace: "pre-wrap", margin: 0 }}>{JSON.stringify(session.input, null, 2)}</pre>
        </Tabs.TabPane>
        <Tabs.TabPane tab="Output" itemKey="output">
          <pre data-testid="microflow-trace-output-json" style={{ whiteSpace: "pre-wrap", margin: 0 }}>{JSON.stringify(session.output, null, 2)}</pre>
        </Tabs.TabPane>
        <Tabs.TabPane tab="Logs" itemKey="logs">
          <pre style={{ whiteSpace: "pre-wrap", margin: 0 }}>{JSON.stringify(session.logs, null, 2)}</pre>
        </Tabs.TabPane>
        <Tabs.TabPane tab="Errors" itemKey="errors">
          {errors.length === 0 ? <Empty title="No runtime errors" /> : (
            <Space vertical align="start" style={{ width: "100%" }}>
              {errors.map((error, index) => (
                <Card key={`${error.code}:${index}`} style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
                  <Text strong type="danger">{error.code}</Text>
                  <br />
                  <Text>{error.message}</Text>
                  <br />
                  <Text type="tertiary" size="small">{[error.objectId, error.actionId, error.flowId, error.microflowId].filter(Boolean).join(" · ")}</Text>
                  {onSelectError ? <><br /><Button size="small" onClick={() => onSelectError(error)}>定位到节点/连线</Button></> : null}
                </Card>
              ))}
            </Space>
          )}
        </Tabs.TabPane>
      </Tabs>
      {activeFrame ? (
        <Card style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
          <Text strong>Active Frame</Text>
          <pre style={{ whiteSpace: "pre-wrap", margin: 0 }}>{JSON.stringify(activeFrame, null, 2)}</pre>
        </Card>
      ) : null}
    </Space>
  );
}

import { useState } from "react";
import { Button, Card, List, Space, Tag, TextArea, Typography } from "@douyinfe/semi-ui";

const { Text } = Typography;
type DebugCommand = "continue" | "pause" | "stepOver" | "stepInto" | "stepOut" | "runToNode" | "cancel" | "stop";

export interface DebugVariableSnapshot {
  name: string;
  valuePreview: string;
  scope?: string;
}

export interface DebugCallStackFrame {
  id: string;
  name: string;
}

export interface DebugBranchFrame {
  branchId: string;
  status: string;
}

export interface DebugBreakpointView {
  id: string;
  targetId: string;
  scope: "node" | "flow" | "expression" | "error" | "gatewayBranch";
  stale?: boolean;
  condition?: string;
  hitTarget?: number;
  logpoint?: boolean;
}

export interface MicroflowStepDebugPanelProps {
  status: string;
  currentNodeId?: string;
  currentFlowId?: string;
  currentBranchId?: string;
  variables?: DebugVariableSnapshot[];
  watches?: Array<{ expression: string; value?: string; error?: string }>;
  callStack?: DebugCallStackFrame[];
  branches?: DebugBranchFrame[];
  breakpoints?: DebugBreakpointView[];
  labels: MicroflowStepDebugPanelLabels;
  onCommand?: (command: DebugCommand) => void;
  onEvaluate?: (expression: string) => void;
}

export interface MicroflowStepDebugPanelLabels {
  statusPrefix: string;
  nodePrefix: string;
  flowPrefix: string;
  branchPrefix: string;
  breakpointsTitle: string;
  staleBreakpoint: string;
  logpoint: string;
  variablesTitle: string;
  watchesTitle: string;
  callStackTitle: string;
  branchTreeTitle: string;
  watchPlaceholder: string;
  evaluate: string;
  commands: Record<DebugCommand, string>;
}

export function MicroflowStepDebugPanel({
  status,
  currentNodeId,
  currentFlowId,
  currentBranchId,
  variables = [],
  watches = [],
  callStack = [],
  branches = [],
  breakpoints = [],
  labels,
  onCommand,
  onEvaluate,
}: MicroflowStepDebugPanelProps) {
  const [watch, setWatch] = useState("");
  const commands: DebugCommand[] = [
    "continue",
    "pause",
    "stepOver",
    "stepInto",
    "stepOut",
    "runToNode",
    "cancel",
    "stop",
  ];
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <Space wrap>
        <Tag color="blue">{labels.statusPrefix}: {status}</Tag>
        {currentNodeId ? <Tag color="green">{labels.nodePrefix}: {currentNodeId}</Tag> : null}
        {currentFlowId ? <Tag color="cyan">{labels.flowPrefix}: {currentFlowId}</Tag> : null}
        {currentBranchId ? <Tag color="orange">{labels.branchPrefix}: {currentBranchId}</Tag> : null}
      </Space>
      <Space wrap>
        {commands.map(command => (
          <Button key={command} onClick={() => onCommand?.(command)}>
            {labels.commands[command]}
          </Button>
        ))}
      </Space>
      <Card title={labels.variablesTitle}>
        <List dataSource={variables} renderItem={item => <List.Item>{item.name}: {item.valuePreview}</List.Item>} />
      </Card>
      <Card title={labels.watchesTitle}>
        <Space vertical align="start" style={{ width: "100%" }}>
          <TextArea value={watch} onChange={setWatch} placeholder={labels.watchPlaceholder} />
          <Button onClick={() => onEvaluate?.(watch)}>{labels.evaluate}</Button>
          {watches.map(item => (
            <Text key={item.expression} type={item.error ? "danger" : "tertiary"}>{item.expression}: {item.error ?? item.value}</Text>
          ))}
        </Space>
      </Card>
      <Card title={labels.breakpointsTitle}>
        <List
          dataSource={breakpoints}
          renderItem={item => (
            <List.Item>
              <Text type={item.stale ? "tertiary" : "primary"}>
                {item.scope}: {item.targetId}
                {item.condition ? ` (${item.condition})` : ""}
                {item.hitTarget ? ` #${item.hitTarget}` : ""}
                {item.logpoint ? ` ${labels.logpoint}` : ""}
                {item.stale ? ` ${labels.staleBreakpoint}` : ""}
              </Text>
            </List.Item>
          )}
        />
      </Card>
      <Card title={labels.callStackTitle}>
        <List dataSource={callStack} renderItem={item => <List.Item>{item.name}</List.Item>} />
      </Card>
      <Card title={labels.branchTreeTitle}>
        <List dataSource={branches} renderItem={item => <List.Item>{item.branchId}: {item.status}</List.Item>} />
      </Card>
    </Space>
  );
}

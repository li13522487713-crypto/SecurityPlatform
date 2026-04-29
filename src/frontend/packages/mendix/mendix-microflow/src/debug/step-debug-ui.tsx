import { useState } from "react";
import { Button, Card, List, Space, Tag, TextArea, Typography } from "@douyinfe/semi-ui";

const { Text } = Typography;

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

export interface MicroflowStepDebugPanelProps {
  status: string;
  currentNodeId?: string;
  variables?: DebugVariableSnapshot[];
  watches?: Array<{ expression: string; value?: string; error?: string }>;
  callStack?: DebugCallStackFrame[];
  branches?: DebugBranchFrame[];
  onCommand?: (command: "continue" | "pause" | "stepOver" | "stepInto" | "stepOut" | "runToNode" | "cancel" | "stop") => void;
  onEvaluate?: (expression: string) => void;
}

export function MicroflowStepDebugPanel({
  status,
  currentNodeId,
  variables = [],
  watches = [],
  callStack = [],
  branches = [],
  onCommand,
  onEvaluate,
}: MicroflowStepDebugPanelProps) {
  const [watch, setWatch] = useState("");
  const commands: Array<{ command: NonNullable<Parameters<NonNullable<MicroflowStepDebugPanelProps["onCommand"]>>[0]>; label: string }> = [
    { command: "continue", label: "Continue" },
    { command: "pause", label: "Pause" },
    { command: "stepOver", label: "Step Over" },
    { command: "stepInto", label: "Step Into" },
    { command: "stepOut", label: "Step Out" },
    { command: "runToNode", label: "Run to Node" },
    { command: "cancel", label: "Cancel" },
    { command: "stop", label: "Stop" },
  ];
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <Space wrap>
        <Tag color="blue">Debug: {status}</Tag>
        {currentNodeId ? <Tag color="green">Node: {currentNodeId}</Tag> : null}
      </Space>
      <Space wrap>
        {commands.map(({ command, label }) => (
          <Button key={command} onClick={() => onCommand?.(command)}>
            {label}
          </Button>
        ))}
      </Space>
      <Card title="Variables">
        <List dataSource={variables} renderItem={item => <List.Item>{item.name}: {item.valuePreview}</List.Item>} />
      </Card>
      <Card title="Watches">
        <Space vertical align="start" style={{ width: "100%" }}>
          <TextArea value={watch} onChange={setWatch} placeholder="$latestError or $variable.member" />
          <Button onClick={() => onEvaluate?.(watch)}>Evaluate</Button>
          {watches.map(item => (
            <Text key={item.expression} type={item.error ? "danger" : "tertiary"}>{item.expression}: {item.error ?? item.value}</Text>
          ))}
        </Space>
      </Card>
      <Card title="Call stack">
        <List dataSource={callStack} renderItem={item => <List.Item>{item.name}</List.Item>} />
      </Card>
      <Card title="Branch tree">
        <List dataSource={branches} renderItem={item => <List.Item>{item.branchId}: {item.status}</List.Item>} />
      </Card>
    </Space>
  );
}

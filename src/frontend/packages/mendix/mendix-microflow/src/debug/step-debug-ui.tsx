import { useEffect, useState } from "react";
import { Button, Card, List, Space, Tag, TextArea, Tooltip, Typography } from "@douyinfe/semi-ui";
import { DebugCallStackPanel } from "../components/DebugCallStackPanel";
import { DebugBreakpointsPanel } from "../components/DebugBreakpointsPanel";
import { DebugVariablesPanel } from "../components/DebugVariablesPanel";

const { Text } = Typography;
type DebugCommand = "continue" | "pause" | "stepOver" | "stepInto" | "stepOut" | "runToNode" | "cancel" | "stop";

export interface DebugVariableSnapshot {
  name: string;
  valuePreview: string;
  type?: string;
  scope?: string;
}

export interface DebugCallStackFrame {
  id: string;
  name: string;
  microflowId?: string;
  runId?: string;
  depth?: number;
  status?: string;
  currentNodeCaption?: string;
}

export interface DebugBranchFrame {
  branchId: string;
  status: string;
}

export interface DebugLoopIterationView {
  nodeId?: string;
  iterationIndex?: number;
  totalIterations?: number;
}

export interface DebugBreakpointView {
  id: string;
  targetId: string;
  scope: "node" | "flow" | "expression" | "error" | "gatewayBranch";
  stale?: boolean;
  condition?: string;
  hitTarget?: number;
  logpoint?: boolean;
  enabled?: boolean;
  kind?: "basic" | "conditional";
}

export interface MicroflowStepDebugPanelProps {
  status: string;
  currentNodeId?: string;
  currentFlowId?: string;
  currentBranchId?: string;
  currentPhase?: string;
  activeError?: string;
  activeErrorStack?: string;
  variables?: DebugVariableSnapshot[];
  activeVariableName?: string;
  watches?: Array<{ expression: string; value?: string; error?: string }>;
  callStack?: DebugCallStackFrame[];
  branches?: DebugBranchFrame[];
  loopIteration?: DebugLoopIterationView;
  breakpoints?: DebugBreakpointView[];
  labels: MicroflowStepDebugPanelLabels;
  onCommand?: (command: DebugCommand) => void;
  onEvaluate?: (expression: string) => void;
  onVariableSelect?: (variableName: string) => void;
  onCallStackFrameClick?: (frame: DebugCallStackFrame, index: number) => void;
  onBreakpointToggle?: (breakpointId: string, enabled: boolean) => void;
  onBreakpointDelete?: (breakpointId: string) => void;
  onBreakpointConditionChange?: (breakpointId: string, condition: string) => void;
}

export interface MicroflowStepDebugPanelLabels {
  statusPrefix: string;
  nodePrefix: string;
  flowPrefix: string;
  branchPrefix: string;
  phasePrefix: string;
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

function isPausedStatus(status: string): boolean {
  return status.toLowerCase().includes("paused");
}

function isTerminalStatus(status: string): boolean {
  const normalized = status.toLowerCase();
  return normalized.includes("cancelled")
    || normalized.includes("completed")
    || normalized.includes("failed")
    || normalized.includes("stopped");
}

function commandDisabledReason(command: DebugCommand, status: string): string {
  if (isTerminalStatus(status)) {
    return "Debug session is already finished.";
  }
  if (command === "pause") {
    return isPausedStatus(status) ? "Session is already paused." : "";
  }
  if (command === "continue" || command === "stepOver" || command === "stepInto" || command === "stepOut" || command === "runToNode") {
    return isPausedStatus(status) ? "" : "This command is available only when paused.";
  }
  return "";
}

function normalizeCallStackSegment(name: string): string {
  const text = String(name ?? "").trim();
  const match = text.match(/^\d+:(.+)$/);
  return match?.[1]?.trim() || text;
}

function sortCallStackFrames(frames: DebugCallStackFrame[]): DebugCallStackFrame[] {
  if (!frames.length) {
    return [];
  }
  const withDepth = frames.filter(frame => Number.isFinite(frame.depth));
  if (withDepth.length !== frames.length) {
    return [...frames];
  }
  return [...frames].sort((left, right) => Number(left.depth) - Number(right.depth));
}

export function MicroflowStepDebugPanel({
  status,
  currentNodeId,
  currentFlowId,
  currentBranchId,
  currentPhase,
  activeError,
  activeErrorStack,
  variables = [],
  activeVariableName,
  watches = [],
  callStack = [],
  branches = [],
  loopIteration,
  breakpoints = [],
  labels,
  onCommand,
  onEvaluate,
  onVariableSelect,
  onCallStackFrameClick,
  onBreakpointToggle,
  onBreakpointDelete,
  onBreakpointConditionChange,
}: MicroflowStepDebugPanelProps) {
  const [watch, setWatch] = useState("");
  const [showErrorStack, setShowErrorStack] = useState(false);
  const orderedCallStack = sortCallStackFrames(callStack);
  const callStackPath = orderedCallStack.map(frame => normalizeCallStackSegment(frame.name)).filter(Boolean).join(" > ");
  useEffect(() => {
    if (!activeErrorStack) {
      setShowErrorStack(false);
    }
  }, [activeErrorStack]);
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
        {currentPhase ? <Tag color="purple">{labels.phasePrefix}: {currentPhase}</Tag> : null}
        {activeError ? <Tag color="red" data-testid="microflow-debug-error">{activeError}</Tag> : null}
        {loopIteration?.iterationIndex != null || loopIteration?.totalIterations != null ? (
          <Tag color="orange" data-testid="microflow-debug-loop-iteration">
            第 {loopIteration?.iterationIndex ?? "-"} / {loopIteration?.totalIterations ?? "-"} 次
          </Tag>
        ) : null}
      </Space>
      {activeErrorStack ? (
        <Space wrap>
          <Button
            size="small"
            theme="borderless"
            data-testid="microflow-debug-toggle-stack"
            onClick={() => setShowErrorStack(current => !current)}
          >
            {showErrorStack ? "Hide stack trace" : "View stack trace"}
          </Button>
        </Space>
      ) : null}
      {activeErrorStack && showErrorStack ? (
        <Card title="Stack Trace" style={{ width: "100%" }}>
          <TextArea value={activeErrorStack} readOnly autosize data-testid="microflow-debug-stacktrace" />
        </Card>
      ) : null}
      <Space wrap>
        {commands.map(command => {
          const disabledReason = commandDisabledReason(command, status);
          return (
            <Tooltip key={command} content={disabledReason || labels.commands[command]}>
              <span style={{ display: "inline-flex" }}>
                <Button disabled={Boolean(disabledReason)} onClick={() => onCommand?.(command)}>
                  {labels.commands[command]}
                </Button>
              </span>
            </Tooltip>
          );
        })}
      </Space>
      <DebugVariablesPanel
        title={labels.variablesTitle}
        variables={variables}
        activeVariableName={activeVariableName}
        onSelectVariable={onVariableSelect}
      />
      <Card title={labels.watchesTitle}>
        <Space vertical align="start" style={{ width: "100%" }}>
          <TextArea value={watch} onChange={setWatch} placeholder={labels.watchPlaceholder} />
          <Button onClick={() => onEvaluate?.(watch)}>{labels.evaluate}</Button>
          {watches.map(item => (
            <Text key={item.expression} type={item.error ? "danger" : "tertiary"}>{item.expression}: {item.error ?? item.value}</Text>
          ))}
        </Space>
      </Card>
      <DebugBreakpointsPanel
        title={labels.breakpointsTitle}
        breakpoints={breakpoints}
        staleBreakpointLabel={labels.staleBreakpoint}
        logpointLabel={labels.logpoint}
        onToggleEnabled={onBreakpointToggle}
        onDelete={onBreakpointDelete}
        onChangeCondition={onBreakpointConditionChange}
      />
      <Card title={labels.callStackTitle}>
        {callStackPath ? (
          <Text type="tertiary" data-testid="microflow-debug-callstack-path">{callStackPath}</Text>
        ) : null}
        <DebugCallStackPanel frames={orderedCallStack} onSelectFrame={onCallStackFrameClick} />
      </Card>
      <Card title={labels.branchTreeTitle}>
        <List dataSource={branches} renderItem={item => <List.Item>{item.branchId}: {item.status}</List.Item>} />
      </Card>
    </Space>
  );
}

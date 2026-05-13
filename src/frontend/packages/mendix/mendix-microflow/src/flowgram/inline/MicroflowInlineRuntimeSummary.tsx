import { useState } from "react";
import { Tag, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconCopy } from "@douyinfe/semi-icons";

import type { MicroflowRuntimeVariableValue, MicroflowTraceFrame } from "../../debug/trace-types";
import { buildMicroflowNodeIoViewModel } from "../../debug/node-io-view-model";

const { Text } = Typography;

function CopyButton({ value }: { value: string | undefined }) {
  const [copied, setCopied] = useState(false);
  if (!value) return null;
  return (
    <Tooltip content={copied ? "已复制" : "复制值"} position="top">
      <button
        type="button"
        aria-label="复制值"
        style={{
          marginLeft: 4,
          padding: "1px 3px",
          border: "none",
          borderRadius: 3,
          background: "transparent",
          color: copied ? "#28c786" : "#94a3b8",
          cursor: "pointer",
          display: "inline-flex",
          alignItems: "center",
          flexShrink: 0,
        }}
        onClick={() => {
          navigator.clipboard.writeText(value).then(() => {
            setCopied(true);
            Toast.success({ content: "已复制到剪贴板", duration: 1.5 });
            setTimeout(() => setCopied(false), 1800);
          }).catch(() => undefined);
        }}
      >
        <IconCopy size="extra-small" />
      </button>
    </Tooltip>
  );
}

export interface MicroflowInlineRuntimeSummaryProps {
  frame: MicroflowTraceFrame;
  expanded: boolean;
}

function getStatusTagColor(status: MicroflowTraceFrame["status"]): "red" | "orange" | "grey" | "green" | "blue" {
  switch (status) {
    case "failed":
      return "red";
    case "unsupported":
      return "orange";
    case "skipped":
      return "grey";
    case "success":
      return "green";
    case "running":
      return "blue";
    default:
      return "grey";
  }
}

function VariableRow({ label, vars }: { label: string; vars: Record<string, MicroflowRuntimeVariableValue> }) {
  const entries = Object.entries(vars);
  if (entries.length === 0) return null;
  return (
    <>
      <div className="microflow-flowgram-node__runtime-summary-row">
        <span className="microflow-flowgram-node__runtime-summary-key">{label}:</span>
      </div>
      {entries.map(([key, variable]) => (
        <div key={key} className="microflow-flowgram-node__runtime-summary-row" style={{ display: "flex", alignItems: "center", gap: 2 }}>
          <span className="microflow-flowgram-node__runtime-summary-key">{variable.name}</span>
          <span
            className="microflow-flowgram-node__runtime-summary-value"
            title={variable.rawValueJson ?? variable.valuePreview}
            style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
          >
            {variable.valuePreview}
          </span>
          <CopyButton value={variable.rawValueJson ?? variable.valuePreview} />
        </div>
      ))}
    </>
  );
}

export function MicroflowInlineRuntimeSummary({ frame, expanded }: MicroflowInlineRuntimeSummaryProps) {
  const viewModel = buildMicroflowNodeIoViewModel(frame);
  const { status, durationMs } = viewModel.summary;
  const { inputVariables } = viewModel.input;
  const { outputVariables } = viewModel.output;

  // 摘要态：只显示 status tag 和耗时
  if (!expanded) {
    return (
      <div className="microflow-flowgram-node__runtime-summary">
        <Tag color={getStatusTagColor(status)} size="small">
          {status}
        </Tag>
        <Text type="tertiary" size="small">
          {durationMs}ms
        </Text>
      </div>
    );
  }

  // 展开态：显示输入/输出变量
  return (
    <div className="microflow-flowgram-node__runtime-summary">
      <div className="microflow-flowgram-node__runtime-summary-io">
        {inputVariables ? <VariableRow label="Input" vars={inputVariables} /> : null}
        {outputVariables ? <VariableRow label="Output" vars={outputVariables} /> : null}
      </div>
    </div>
  );
}

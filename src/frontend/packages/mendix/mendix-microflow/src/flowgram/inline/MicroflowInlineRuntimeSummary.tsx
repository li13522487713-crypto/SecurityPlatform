import { Tag, Typography } from "@douyinfe/semi-ui";

import type { MicroflowTraceFrame } from "../../debug/trace-types";
import { buildMicroflowNodeIoViewModel } from "../../debug/node-io-view-model";

const { Text } = Typography;

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

export function MicroflowInlineRuntimeSummary({ frame, expanded }: MicroflowInlineRuntimeSummaryProps) {
  const viewModel = buildMicroflowNodeIoViewModel(frame);
  const { status, durationMs } = viewModel.summary;
  const { inputVariables, outputVariables } = viewModel.output;

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
        {/* 输入变量 */}
        {inputVariables && Object.entries(inputVariables).length > 0 ? (
          <>
            <div className="microflow-flowgram-node__runtime-summary-row">
              <span className="microflow-flowgram-node__runtime-summary-key">Input:</span>
            </div>
            {Object.entries(inputVariables).map(([key, variable]) => (
              <div key={`input-${key}`} className="microflow-flowgram-node__runtime-summary-row">
                <span className="microflow-flowgram-node__runtime-summary-key">{variable.name}</span>
                <span className="microflow-flowgram-node__runtime-summary-value" title={variable.rawValueJson ?? variable.valuePreview}>
                  {variable.valuePreview}
                </span>
              </div>
            ))}
          </>
        ) : null}

        {/* 输出变量 */}
        {outputVariables && Object.entries(outputVariables).length > 0 ? (
          <>
            <div className="microflow-flowgram-node__runtime-summary-row">
              <span className="microflow-flowgram-node__runtime-summary-key">Output:</span>
            </div>
            {Object.entries(outputVariables).map(([key, variable]) => (
              <div key={`output-${key}`} className="microflow-flowgram-node__runtime-summary-row">
                <span className="microflow-flowgram-node__runtime-summary-key">{variable.name}</span>
                <span className="microflow-flowgram-node__runtime-summary-value" title={variable.rawValueJson ?? variable.valuePreview}>
                  {variable.valuePreview}
                </span>
              </div>
            ))}
          </>
        ) : null}
      </div>
    </div>
  );
}

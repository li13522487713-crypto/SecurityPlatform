import type { ReactNode } from "react";
import { Banner, Button, DatePicker, Input, InputNumber, Select, Spin, Tag, Typography } from "@douyinfe/semi-ui";
import type { AppBuilderConfig, AppInputComponent, AppOutputComponent, WorkbenchTrace } from "../types";
import { resolveOutputValue } from "./app-builder-helpers";

function formatUnknown(value: unknown): string {
  if (value === undefined) {
    return "";
  }
  if (value === null) {
    return "null";
  }
  if (typeof value === "string") {
    return value;
  }
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

function renderOutputBody(
  type: AppOutputComponent["type"],
  raw: unknown
): ReactNode {
  if (raw === undefined) {
    return <Typography.Text type="tertiary">（无数据）</Typography.Text>;
  }
  switch (type) {
    case "text":
      return <Typography.Text style={{ whiteSpace: "pre-wrap" }}>{formatUnknown(raw)}</Typography.Text>;
    case "markdown":
      return (
        <pre className="module-studio__message-content" style={{ margin: 0, whiteSpace: "pre-wrap" }}>
          {formatUnknown(raw)}
        </pre>
      );
    case "json":
      return <pre className="module-studio__message-content">{formatUnknown(raw)}</pre>;
    case "table":
    case "chart":
      return <pre className="module-studio__message-content">{formatUnknown(raw)}</pre>;
    default:
      return <Typography.Text>{formatUnknown(raw)}</Typography.Text>;
  }
}

export interface AppPreviewPanelProps {
  layoutMode: AppBuilderConfig["layoutMode"];
  inputs: AppInputComponent[];
  outputs: AppOutputComponent[];
  previewValues: Record<string, unknown>;
  onPreviewValuesChange: (next: Record<string, unknown>) => void;
  runResult: { outputs: Record<string, unknown>; trace?: WorkbenchTrace } | null;
  running: boolean;
  onRun: () => void;
  disabled?: boolean;
}

export function AppPreviewPanel({
  layoutMode,
  inputs,
  outputs,
  previewValues,
  onPreviewValuesChange,
  runResult,
  running,
  onRun,
  disabled
}: AppPreviewPanelProps) {
  function setKey(key: string, value: unknown) {
    onPreviewValuesChange({ ...previewValues, [key]: value });
  }

  function renderInputControl(row: AppInputComponent) {
    const key = row.variableKey.trim();
    if (!key) {
      return <Typography.Text type="tertiary">请先填写变量键</Typography.Text>;
    }
    const current = previewValues[key];

    switch (row.type) {
      case "textarea":
        return (
          <textarea
            className="module-studio__textarea"
            rows={4}
            disabled={disabled}
            value={typeof current === "string" ? current : current === undefined || current === null ? "" : String(current)}
            onChange={event => setKey(key, event.target.value)}
            placeholder={row.label || key}
          />
        );
      case "number":
        return (
          <InputNumber
            disabled={disabled}
            style={{ width: "100%" }}
            value={typeof current === "number" ? current : undefined}
            onChange={v => setKey(key, typeof v === "number" ? v : undefined)}
            placeholder={row.label || key}
          />
        );
      case "date": {
        const dateVal =
          current instanceof Date
            ? current
            : typeof current === "string" && current
              ? new Date(current)
              : undefined;
        return (
          <DatePicker
            disabled={disabled}
            style={{ width: "100%" }}
            type="date"
            value={dateVal && !Number.isNaN(dateVal.getTime()) ? dateVal : undefined}
            onChange={d => setKey(key, d instanceof Date ? d.toISOString() : undefined)}
            placeholder={row.label || key}
          />
        );
      }
      case "select":
        return (
          <Select
            disabled={disabled}
            style={{ width: "100%" }}
            placeholder={row.label || key}
            value={typeof current === "string" ? current : undefined}
            optionList={(row.options ?? []).map(o => ({ label: o.label, value: o.value }))}
            onChange={v => setKey(key, typeof v === "string" ? v : undefined)}
          />
        );
      case "file":
        return (
          <div className="module-studio__field">
            <input
              type="file"
              disabled={disabled}
              onChange={event => {
                const file = event.target.files?.[0];
                setKey(key, file?.name ?? "");
              }}
            />
            {typeof current === "string" && current ? (
              <Typography.Text type="tertiary" size="small">
                已选：{current}
              </Typography.Text>
            ) : null}
          </div>
        );
      case "text":
      default:
        return (
          <Input
            disabled={disabled}
            value={typeof current === "string" ? current : current === undefined || current === null ? "" : String(current)}
            onChange={v => setKey(key, v)}
            placeholder={row.label || key}
          />
        );
    }
  }

  return (
    <div className="module-studio__app-preview-root module-studio__coze-inspector-card module-studio__app-builder-preview">
      <div className="module-studio__app-preview-hero">
        <div className="module-studio__card-head module-studio__app-preview-hero-head">
          <div>
            <div className="module-studio__app-preview-kicker">实时预览</div>
            <span className="module-studio__app-preview-title">预览与运行</span>
          </div>
          <Tag color="blue">{layoutMode === "form" ? "表单" : layoutMode === "chat" ? "对话" : "混合"}</Tag>
        </div>
        <Banner
          type="info"
          bordered={false}
          fullMode={false}
          title="说明"
          description="左侧配置保存后，可在此填写预览值并运行，查看输出映射结果与执行轨迹摘要。"
        />
      </div>

      <div className="module-studio__app-builder-preview-section module-studio__app-preview-section">
        <div className="module-studio__app-preview-section-head">
          <Typography.Title heading={6} style={{ margin: 0 }}>
            表单预览
          </Typography.Title>
        </div>
        {inputs.length === 0 ? (
          <Typography.Text type="tertiary">尚未配置输入组件。</Typography.Text>
        ) : (
          <div className="module-studio__stack module-studio__app-preview-form-stack">
            {inputs.map(row => (
              <div key={row.id} className="module-studio__field module-studio__app-preview-field">
                <span>
                  {row.label || row.variableKey || "未命名"}
                  {row.required ? <Tag color="orange" size="small" style={{ marginLeft: 6 }}>必填</Tag> : null}
                </span>
                {renderInputControl(row)}
              </div>
            ))}
          </div>
        )}
        <div className="module-studio__app-preview-run-row">
          <Button theme="solid" type="primary" loading={running} disabled={disabled} onClick={() => onRun()}>
            运行预览
          </Button>
        </div>
      </div>

      <div className="module-studio__app-builder-preview-section module-studio__app-preview-section module-studio__app-preview-section--output">
        <div className="module-studio__app-preview-section-head">
          <Typography.Title heading={6} style={{ margin: 0 }}>
            输出结果
          </Typography.Title>
        </div>
        {running ? (
          <div className="module-studio__app-preview-spin">
            <Spin />
          </div>
        ) : runResult ? (
          <div className="module-studio__stack">
            {outputs.length === 0 ? (
              <Typography.Text type="tertiary">未配置输出映射，以下为完整返回对象：</Typography.Text>
            ) : null}
            {outputs.length === 0 ? (
              <pre className="module-studio__message-content">{formatUnknown(runResult.outputs)}</pre>
            ) : (
              outputs.map(out => {
                const raw = resolveOutputValue(out.sourceExpression, runResult.outputs);
                return (
                  <div key={out.id} className="module-studio__coze-inspector-card module-studio__app-preview-output-card">
                    <div className="module-studio__card-head">
                      <strong>{out.label || out.sourceExpression || "输出"}</strong>
                      <Tag size="small">{out.type}</Tag>
                    </div>
                    {renderOutputBody(out.type, raw)}
                  </div>
                );
              })
            )}
            {runResult.trace ? (
              <div className="module-studio__coze-inspector-card module-studio__app-preview-trace-card">
                <div className="module-studio__card-head">
                  <span>执行轨迹</span>
                  <Tag color="green">{runResult.trace.status}</Tag>
                </div>
                <Typography.Text type="tertiary" size="small">
                  {runResult.trace.executionId} · {runResult.trace.durationMs != null ? `${runResult.trace.durationMs} ms` : ""}
                </Typography.Text>
                <div className="module-studio__stack module-studio__app-preview-trace-steps">
                  {runResult.trace.steps.map((step, i) => (
                    <div key={`${step.nodeKey}-${i}`} className="module-studio__coze-linkrow">
                      <div>
                        <strong>{step.nodeKey}</strong>
                        <div className="module-studio__meta">{step.nodeType || step.status}</div>
                      </div>
                      <span>{step.durationMs != null ? `${step.durationMs} ms` : ""}</span>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}
          </div>
        ) : (
          <Typography.Text type="tertiary">尚未运行。点击「运行预览」加载结果。</Typography.Text>
        )}
      </div>
    </div>
  );
}

import type { ReactNode } from "react";
import { Banner, Button, DatePicker, Input, InputNumber, Select, Spin, Tag, Typography } from "@douyinfe/semi-ui";
import type { AppBuilderConfig, AppInputComponent, AppOutputComponent, StudioLocale, WorkbenchTrace } from "../types";
import { resolveOutputValue } from "./app-builder-helpers";
import { getStudioCopy, type StudioCopy } from "../copy";

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
  raw: unknown,
  copy: StudioCopy
): ReactNode {
  if (raw === undefined) {
    return <Typography.Text type="tertiary">{copy.appPreview.noData}</Typography.Text>;
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
  locale: StudioLocale;
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
  disabled,
  locale
}: AppPreviewPanelProps) {
  const copy = getStudioCopy(locale);
  function setKey(key: string, value: unknown) {
    onPreviewValuesChange({ ...previewValues, [key]: value });
  }

  function renderInputControl(row: AppInputComponent) {
    const key = row.variableKey.trim();
    if (!key) {
      return <Typography.Text type="tertiary">{copy.appPreview.fillVariableKeyFirst}</Typography.Text>;
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
                {copy.appPreview.selectedFilePrefix}{current}
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

  const layoutTagText =
    layoutMode === "form"
      ? copy.appPreview.layoutForm
      : layoutMode === "chat"
        ? copy.appPreview.layoutChat
        : copy.appPreview.layoutHybrid;

  return (
    <div className="module-studio__app-preview-root module-studio__coze-inspector-card module-studio__app-builder-preview">
      <div className="module-studio__app-preview-hero">
        <div className="module-studio__card-head module-studio__app-preview-hero-head">
          <div>
            <div className="module-studio__app-preview-kicker">{copy.appPreview.kicker}</div>
            <span className="module-studio__app-preview-title">{copy.appPreview.title}</span>
          </div>
          <Tag color="blue">{layoutTagText}</Tag>
        </div>
        <Banner
          type="info"
          bordered={false}
          fullMode={false}
          title={copy.appPreview.bannerTitle}
          description={copy.appPreview.bannerDescription}
        />
      </div>

      <div className="module-studio__app-builder-preview-section module-studio__app-preview-section">
        <div className="module-studio__app-preview-section-head">
          <Typography.Title heading={6} style={{ margin: 0 }}>
            {copy.appPreview.formPreviewSection}
          </Typography.Title>
        </div>
        {inputs.length === 0 ? (
          <Typography.Text type="tertiary">{copy.appPreview.noInputsHint}</Typography.Text>
        ) : (
          <div className="module-studio__stack module-studio__app-preview-form-stack">
            {inputs.map(row => (
              <div key={row.id} className="module-studio__field module-studio__app-preview-field">
                <span>
                  {row.label || row.variableKey || copy.appPreview.unnamed}
                  {row.required ? <Tag color="orange" size="small" style={{ marginLeft: 6 }}>{copy.appPreview.required}</Tag> : null}
                </span>
                {renderInputControl(row)}
              </div>
            ))}
          </div>
        )}
        <div className="module-studio__app-preview-run-row">
          <Button theme="solid" type="primary" loading={running} disabled={disabled} onClick={() => onRun()}>
            {copy.appPreview.runPreview}
          </Button>
        </div>
      </div>

      <div className="module-studio__app-builder-preview-section module-studio__app-preview-section module-studio__app-preview-section--output">
        <div className="module-studio__app-preview-section-head">
          <Typography.Title heading={6} style={{ margin: 0 }}>
            {copy.appPreview.outputResultSection}
          </Typography.Title>
        </div>
        {running ? (
          <div className="module-studio__app-preview-spin">
            <Spin />
          </div>
        ) : runResult ? (
          <div className="module-studio__stack">
            {outputs.length === 0 ? (
              <Typography.Text type="tertiary">{copy.appPreview.noOutputMappingHint}</Typography.Text>
            ) : null}
            {outputs.length === 0 ? (
              <pre className="module-studio__message-content">{formatUnknown(runResult.outputs)}</pre>
            ) : (
              outputs.map(out => {
                const raw = resolveOutputValue(out.sourceExpression, runResult.outputs);
                return (
                  <div key={out.id} className="module-studio__coze-inspector-card module-studio__app-preview-output-card">
                    <div className="module-studio__card-head">
                      <strong>{out.label || out.sourceExpression || copy.appPreview.outputFallbackTitle}</strong>
                      <Tag size="small">{out.type}</Tag>
                    </div>
                    {renderOutputBody(out.type, raw, copy)}
                  </div>
                );
              })
            )}
            {runResult.trace ? (
              <div className="module-studio__coze-inspector-card module-studio__app-preview-trace-card">
                <div className="module-studio__card-head">
                  <span>{copy.appPreview.traceTitle}</span>
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
          <Typography.Text type="tertiary">{copy.appPreview.notRunHint}</Typography.Text>
        )}
      </div>
    </div>
  );
}

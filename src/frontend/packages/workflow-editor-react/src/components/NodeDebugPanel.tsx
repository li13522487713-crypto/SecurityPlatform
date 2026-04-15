import { Button, Input, Select, Switch } from "antd";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import type { NodeDebugFieldDefinition } from "../editor/node-debug-fields";

interface NodeDebugPanelProps {
  visible: boolean;
  running: boolean;
  nodeOptions: Array<{ value: string; label: string }>;
  selectedNodeKey: string;
  inputJson: string;
  output: string;
  extractedFields: NodeDebugFieldDefinition[];
  extractedValues: Record<string, unknown>;
  onNodeChange: (value: string) => void;
  onInputJsonChange: (value: string) => void;
  onExtractedFieldChange: (path: string, value: unknown) => void;
  onRun: () => void;
  onClose: () => void;
}

function stringifyUnknownValue(value: unknown): string {
  if (typeof value === "string") {
    return value;
  }
  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }
  if (value === null || value === undefined) {
    return "";
  }

  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

export function NodeDebugPanel(props: NodeDebugPanelProps) {
  const { t } = useTranslation();
  const renderedFields = useMemo(() => props.extractedFields, [props.extractedFields]);

  if (!props.visible) {
    return null;
  }

  return (
    <div className="wf-react-debug-panel">
      <div className="wf-react-test-header">
        <span className="wf-react-panel-title">{t("wfUi.debug.title")}</span>
        <Button size="small" onClick={props.onClose}>
          {t("wfUi.debug.close")}
        </Button>
      </div>
      <Select
        size="small"
        value={props.selectedNodeKey}
        options={props.nodeOptions}
        style={{ width: "100%", marginBottom: 8 }}
        onChange={props.onNodeChange}
      />
      {renderedFields.length > 0 ? (
        <div className="wf-react-debug-fields">
          <div className="wf-react-debug-fields-title">{t("wfUi.debug.inputFields")}</div>
          <div className="wf-react-debug-fields-grid">
            {renderedFields.map((field) => {
              const currentValue = props.extractedValues[field.path];
              return (
                <label key={field.path} className="wf-react-debug-field">
                  <span>{field.label}</span>
                  {field.kind === "boolean" ? (
                    <Switch
                      checked={Boolean(currentValue)}
                      onChange={(checked) => props.onExtractedFieldChange(field.path, checked)}
                    />
                  ) : field.kind === "number" ? (
                    <Input
                      value={stringifyUnknownValue(currentValue)}
                      onChange={(event) => props.onExtractedFieldChange(field.path, event.target.value)}
                      placeholder={field.path}
                    />
                  ) : field.kind === "json" ? (
                    <Input.TextArea
                      rows={3}
                      value={stringifyUnknownValue(currentValue)}
                      onChange={(event) => props.onExtractedFieldChange(field.path, event.target.value)}
                      placeholder={field.path}
                    />
                  ) : (
                    <Input
                      value={stringifyUnknownValue(currentValue)}
                      onChange={(event) => props.onExtractedFieldChange(field.path, event.target.value)}
                      placeholder={field.path}
                    />
                  )}
                  <small>{field.path}</small>
                </label>
              );
            })}
          </div>
        </div>
      ) : (
        <div className="wf-react-debug-empty-hint">{t("wfUi.debug.noExtractedFields")}</div>
      )}
      <div className="wf-react-debug-raw-label">{t("wfUi.debug.rawJson")}</div>
      <Input.TextArea rows={6} value={props.inputJson} onChange={(event) => props.onInputJsonChange(event.target.value)} />
      <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 8 }}>
        <Button size="small" type="primary" loading={props.running} onClick={props.onRun}>
          {t("wfUi.debug.run")}
        </Button>
      </div>
      <div className="wf-react-debug-output-label">{t("wfUi.debug.output")}</div>
      <Input.TextArea rows={10} value={props.output} readOnly style={{ marginTop: 8 }} />
    </div>
  );
}

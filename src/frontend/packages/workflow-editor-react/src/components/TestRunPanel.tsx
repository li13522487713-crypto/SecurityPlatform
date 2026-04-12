import { Button, Input, Select, Space, Tag } from "antd";
import { useTranslation } from "react-i18next";

interface TestRunPanelProps {
  visible: boolean;
  logs: string[];
  running: boolean;
  source: "published" | "draft";
  mode: "stream" | "sync";
  inputJson: string;
  onInputJsonChange: (value: string) => void;
  onSourceChange: (value: "published" | "draft") => void;
  onModeChange: (value: "stream" | "sync") => void;
  onClose: () => void;
  onRun: () => void;
}

export function TestRunPanel(props: TestRunPanelProps) {
  const { t } = useTranslation();
  if (!props.visible) {
    return null;
  }

  return (
    <div className="wf-react-test-panel" data-testid="workflow.detail.node.testrun.result-panel">
      <div className="wf-react-test-header">
        <Space>
          <span className="wf-react-panel-title">{t("wfUi.testRun.title")}</span>
          <Tag color={props.mode === "stream" ? "blue" : "default"}>{t("wfUi.testRun.stream")}</Tag>
          <Tag color={props.mode === "sync" ? "green" : "default"}>{t("wfUi.testRun.sync")}</Tag>
          <Select<"stream" | "sync">
            size="small"
            value={props.mode}
            onChange={props.onModeChange}
            options={[
              { value: "stream", label: t("wfUi.testRun.stream") },
              { value: "sync", label: t("wfUi.testRun.sync") }
            ]}
            style={{ width: 88 }}
          />
          <Select<"published" | "draft">
            size="small"
            value={props.source}
            onChange={props.onSourceChange}
            options={[
              { value: "published", label: t("wfUi.testRun.published") },
              { value: "draft", label: t("wfUi.testRun.draft") }
            ]}
            style={{ width: 120 }}
          />
        </Space>
        <Space>
          <Button size="small" type="primary" loading={props.running} onClick={props.onRun}>
            {props.running ? t("wfUi.testRun.running") : t("wfUi.testRun.run")}
          </Button>
          <Button size="small" onClick={props.onClose}>
            {t("wfUi.testRun.close")}
          </Button>
        </Space>
      </div>
      <Input.TextArea
        rows={5}
        value={props.inputJson}
        onChange={(event) => props.onInputJsonChange(event.target.value)}
        placeholder={t("wfUi.testRun.inputPlaceholder")}
      />
      <div className="wf-react-test-log">
        {props.logs.length === 0 ? (
          <div className="wf-react-test-empty">{t("wfUi.testRun.emptyLog")}</div>
        ) : (
          props.logs.map((line, index) => (
            <div key={`${line}-${index}`} data-testid="workflow.detail.node.testrun.result-item">
              {line}
            </div>
          ))
        )}
      </div>
    </div>
  );
}


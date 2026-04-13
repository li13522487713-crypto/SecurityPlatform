import { Alert, Button, Input, Select, Space, Tag } from "antd";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { validateTestRunPayload, type TestRunValidationIssueCode } from "../editor/test-run-validation";

interface TestRunPanelProps {
  visible: boolean;
  logs: string[];
  running: boolean;
  workflowMode?: "workflow" | "chatflow";
  source: "published" | "draft";
  mode: "stream" | "sync";
  inputJson: string;
  onInputJsonChange: (value: string) => void;
  onSourceChange: (value: "published" | "draft") => void;
  onModeChange: (value: "stream" | "sync") => void;
  onClose: () => void;
  onRun: () => void;
}

function getValidationIssueLabel(t: (key: string) => string, issue: TestRunValidationIssueCode): string {
  switch (issue) {
    case "invalidJson":
      return t("wfUi.testRun.invalidJson");
    case "invalidPayload":
      return t("wfUi.testRun.invalidPayload");
    case "userInputRequired":
      return t("wfUi.testRun.userInputRequired");
    case "conversationNameRequired":
      return t("wfUi.testRun.conversationNameRequired");
    case "conversationNameTooLong":
      return t("wfUi.testRun.conversationNameTooLong");
    default:
      return issue;
  }
}

export function TestRunPanel(props: TestRunPanelProps) {
  const { t } = useTranslation();
  const [userInput, setUserInput] = useState("");
  const [conversationName, setConversationName] = useState("");

  const validation = useMemo(() => validateTestRunPayload(props.inputJson, props.workflowMode), [props.inputJson, props.workflowMode]);
  const parsedPayload = validation.payload;
  const parsedEntries = useMemo(() => Object.entries(parsedPayload), [parsedPayload]);
  const validationMessages = useMemo(() => validation.issues.map((issue) => getValidationIssueLabel(t, issue)), [t, validation.issues]);

  useEffect(() => {
    if (props.workflowMode !== "chatflow") {
      return;
    }

    const nextUserInput =
      typeof parsedPayload.USER_INPUT === "string"
        ? parsedPayload.USER_INPUT
        : typeof parsedPayload.input === "string"
          ? parsedPayload.input
          : "";
    const nextConversationName =
      typeof parsedPayload.CONVERSATION_NAME === "string" ? parsedPayload.CONVERSATION_NAME : "";
    setUserInput(nextUserInput);
    setConversationName(nextConversationName);
  }, [parsedPayload, props.workflowMode]);

  function updateChatflowInput(next: { userInput?: string; conversationName?: string }) {
    const nextPayload = {
      ...parsedPayload,
      USER_INPUT: next.userInput ?? userInput,
      CONVERSATION_NAME: next.conversationName ?? conversationName
    };
    props.onInputJsonChange(JSON.stringify(nextPayload, null, 2));
  }

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
          <Button size="small" type="primary" loading={props.running} onClick={props.onRun} disabled={validation.issues.length > 0}>
            {props.running ? t("wfUi.testRun.running") : t("wfUi.testRun.run")}
          </Button>
          <Button size="small" onClick={props.onClose}>
            {t("wfUi.testRun.close")}
          </Button>
        </Space>
      </div>
      {validationMessages.length > 0 ? (
        <Alert
          className="wf-react-test-validation"
          type="error"
          showIcon
          message={t("wfUi.testRun.validationTitle")}
          description={
            <ul className="wf-react-test-validation-list">
              {validationMessages.map((issue) => (
                <li key={issue}>{issue}</li>
              ))}
            </ul>
          }
        />
      ) : null}
      <Input.TextArea
        rows={5}
        value={props.inputJson}
        onChange={(event) => props.onInputJsonChange(event.target.value)}
        placeholder={t("wfUi.testRun.inputPlaceholder")}
      />
      {props.workflowMode === "chatflow" ? (
        <div className="wf-react-test-chatflow-fields">
          <label className="wf-react-test-chatflow-field">
            <span>{t("wfUi.testRun.userInputLabel")}</span>
            <Input
              value={userInput}
              onChange={(event) => {
                const nextValue = event.target.value;
                setUserInput(nextValue);
                updateChatflowInput({ userInput: nextValue });
              }}
              placeholder={t("wfUi.testRun.userInputPlaceholder")}
            />
          </label>
          <label className="wf-react-test-chatflow-field">
            <span>{t("wfUi.testRun.conversationNameLabel")}</span>
            <Input
              value={conversationName}
              onChange={(event) => {
                const nextValue = event.target.value;
                setConversationName(nextValue);
                updateChatflowInput({ conversationName: nextValue });
              }}
              placeholder={t("wfUi.testRun.conversationNamePlaceholder")}
            />
          </label>
        </div>
      ) : null}
      <div className="wf-react-test-payload-preview">
        <div className="wf-react-test-preview-head">
          <span className="wf-react-panel-title">{t("wfUi.testRun.payloadTitle")}</span>
          <Tag color="blue">{parsedEntries.length}</Tag>
        </div>
        {parsedEntries.length === 0 ? (
          <div className="wf-react-test-preview-empty">{t("wfUi.testRun.payloadEmpty")}</div>
        ) : (
          <div className="wf-react-test-preview-grid">
            {parsedEntries.map(([key, value]) => (
              <div key={key} className="wf-react-test-preview-item">
                <strong>{key}</strong>
                <span>{typeof value === "string" ? value : JSON.stringify(value)}</span>
              </div>
            ))}
          </div>
        )}
      </div>
      <div className="wf-react-test-log">
        {props.logs.length === 0 ? (
          <div className="wf-react-test-empty">{t("wfUi.testRun.emptyLog")}</div>
        ) : (
          props.logs.map((line, index) => (
            <div key={`${line}-${index}`} className="wf-react-test-log-line" data-testid="workflow.detail.node.testrun.result-item">
              <span className="wf-react-test-log-index">{String(index + 1).padStart(2, "0")}</span>
              <span className="wf-react-test-log-text">{line}</span>
            </div>
          ))
        )}
      </div>
    </div>
  );
}


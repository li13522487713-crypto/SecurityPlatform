import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

interface VariablePanelProps {
  visible: boolean;
  variables: Array<{ key: string; label: string; source: string }>;
  globals: Record<string, unknown>;
  onChangeGlobals: (next: Record<string, unknown>) => void;
  onClose: () => void;
}

export function VariablePanel(props: VariablePanelProps) {
  const { t } = useTranslation();
  const [draftGlobalKey, setDraftGlobalKey] = useState("");
  const [draftGlobalValue, setDraftGlobalValue] = useState("");

  const globalEntries = useMemo(
    () => Object.entries(props.globals ?? {}).sort((a, b) => a[0].localeCompare(b[0])),
    [props.globals]
  );

  function parseUnknownValue(raw: string): unknown {
    const trimmed = raw.trim();
    if (!trimmed) {
      return "";
    }
    try {
      return JSON.parse(trimmed) as unknown;
    } catch {
      return raw;
    }
  }

  function updateGlobalValue(key: string, raw: string) {
    const next = { ...props.globals };
    next[key] = parseUnknownValue(raw);
    props.onChangeGlobals(next);
  }

  function removeGlobalValue(key: string) {
    const next = { ...props.globals };
    delete next[key];
    props.onChangeGlobals(next);
  }

  function addGlobalValue() {
    const normalizedKey = draftGlobalKey.trim();
    if (!normalizedKey) {
      return;
    }
    const next = { ...props.globals };
    next[normalizedKey] = parseUnknownValue(draftGlobalValue);
    props.onChangeGlobals(next);
    setDraftGlobalKey("");
    setDraftGlobalValue("");
  }

  if (!props.visible) {
    return null;
  }

  return (
    <div className="wf-react-variable-panel">
      <div className="wf-react-variable-panel-header">
        <span className="wf-react-panel-title">{t("wfUi.variables.title")}</span>
        <button type="button" onClick={props.onClose}>
          {t("wfUi.variables.close")}
        </button>
      </div>
      <div className="wf-react-global-editor">
        <div className="wf-react-global-title">{t("wfUi.variables.globals")}</div>
        <div className="wf-react-global-list">
          {globalEntries.length === 0 ? <div className="wf-react-variable-path">{t("wfUi.variables.emptyGlobals")}</div> : null}
          {globalEntries.map(([key, value]) => (
            <div key={key} className="wf-react-global-row">
              <input className="wf-react-global-key" value={key} readOnly />
              <input
                className="wf-react-global-value"
                value={typeof value === "string" ? value : JSON.stringify(value)}
                onChange={(event) => updateGlobalValue(key, event.target.value)}
              />
              <button type="button" onClick={() => removeGlobalValue(key)}>
                {t("wfUi.variables.remove")}
              </button>
            </div>
          ))}
        </div>
        <div className="wf-react-global-create">
          <input
            className="wf-react-global-key"
            placeholder={t("wfUi.variables.keyPlaceholder")}
            value={draftGlobalKey}
            onChange={(event) => setDraftGlobalKey(event.target.value)}
          />
          <input
            className="wf-react-global-value"
            placeholder={t("wfUi.variables.valuePlaceholder")}
            value={draftGlobalValue}
            onChange={(event) => setDraftGlobalValue(event.target.value)}
          />
          <button type="button" onClick={addGlobalValue}>
            {t("wfUi.variables.add")}
          </button>
        </div>
      </div>
      <div className="wf-react-variable-list">
        {props.variables.map((item) => (
          <div key={`${item.source}.${item.key}`} className="wf-react-variable-item">
            <div className="wf-react-variable-name">{item.label}</div>
            <div className="wf-react-variable-path">{`{{${item.source}.${item.key}}}`}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

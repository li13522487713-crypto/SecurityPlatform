import { useMemo, useState } from "react";

interface VariablePanelProps {
  visible: boolean;
  variables: Array<{ key: string; label: string; source: string }>;
  globals: Record<string, unknown>;
  onChangeGlobals: (next: Record<string, unknown>) => void;
  onClose: () => void;
}

export function VariablePanel(props: VariablePanelProps) {
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
        <span>变量面板</span>
        <button type="button" onClick={props.onClose}>
          关闭
        </button>
      </div>
      <div className="wf-react-global-editor">
        <div className="wf-react-global-title">全局变量</div>
        <div className="wf-react-global-list">
          {globalEntries.length === 0 ? <div className="wf-react-variable-path">暂无全局变量</div> : null}
          {globalEntries.map(([key, value]) => (
            <div key={key} className="wf-react-global-row">
              <input className="wf-react-global-key" value={key} readOnly />
              <input
                className="wf-react-global-value"
                value={typeof value === "string" ? value : JSON.stringify(value)}
                onChange={(event) => updateGlobalValue(key, event.target.value)}
              />
              <button type="button" onClick={() => removeGlobalValue(key)}>
                删除
              </button>
            </div>
          ))}
        </div>
        <div className="wf-react-global-create">
          <input
            className="wf-react-global-key"
            placeholder="key"
            value={draftGlobalKey}
            onChange={(event) => setDraftGlobalKey(event.target.value)}
          />
          <input
            className="wf-react-global-value"
            placeholder="value"
            value={draftGlobalValue}
            onChange={(event) => setDraftGlobalValue(event.target.value)}
          />
          <button type="button" onClick={addGlobalValue}>
            添加
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

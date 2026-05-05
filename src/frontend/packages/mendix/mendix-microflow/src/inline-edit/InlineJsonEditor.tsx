import { TextArea } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { ContextVariablePicker, type ContextVariableCandidate } from "./shared/ContextVariablePicker";

export function InlineJsonEditor(props: {
  value: string;
  placeholder?: string;
  readonly?: boolean;
  invalid?: boolean;
  options?: Array<{ label: string; value: string }>;
  onCommit?: (value: string) => void;
}) {
  const [draft, setDraft] = useState(props.value);
  const variables: ContextVariableCandidate[] | undefined = props.options?.map(option => {
    const [group, rawMeta] = option.label.includes("::")
      ? option.label.split("::", 2)
      : ["context", option.label];
    const [displayName, ...metadataTokens] = rawMeta.split("|");
    const metadata = Object.fromEntries(
      metadataTokens
        .map(token => token.split("=", 2))
        .filter(item => item.length === 2)
        .map(([k, v]) => [k, v]),
    );
    return {
      name: option.value,
      source: group,
      sourceNode: metadata.sourceNode || displayName,
      scope: metadata.scope,
      readonly: metadata.readonly === "true",
      maybe: metadata.maybe === "true",
      unknown: metadata.unknown === "true",
      preview: metadata.preview,
      type: metadata.type ? { kind: metadata.type } : undefined,
    };
  });

  useEffect(() => {
    setDraft(props.value);
  }, [props.value]);

  return (
    <div style={{ display: "grid", gap: 4 }}>
      <TextArea
        rows={3}
        autosize
        value={draft}
        disabled={props.readonly}
        placeholder={props.placeholder}
        className={props.invalid ? "microflow-inline-field is-invalid" : "microflow-inline-field"}
        onChange={setDraft}
        onKeyDown={event => {
          if ((event.ctrlKey || event.metaKey) && event.key === "Enter") {
            props.onCommit?.(draft);
          }
          if (event.key === "Escape") {
            setDraft(props.value);
          }
        }}
      />
      {variables?.length ? (
        <ContextVariablePicker
          value={draft}
          disabled={props.readonly}
          placeholder="插入变量"
          variables={variables}
          onChange={value => {
            if (!value) {
              return;
            }
            setDraft(current => current.trim() ? `${current} ${value}`.trim() : value);
          }}
        />
      ) : null}
    </div>
  );
}

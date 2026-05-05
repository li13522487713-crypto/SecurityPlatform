import { ContextVariablePicker, type ContextVariableCandidate } from "./shared/ContextVariablePicker";
import { InlineEditableText } from "./InlineEditableText";
import { useEffect, useState } from "react";

export function InlineExpressionField(props: {
  value: string;
  placeholder?: string;
  readonly?: boolean;
  invalid?: boolean;
  options?: Array<{ label: string; value: string }>;
  onCommit?: (value: string) => void;
}) {
  const [draft, setDraft] = useState(props.value);

  useEffect(() => {
    setDraft(props.value);
  }, [props.value]);

  const commit = (next: string) => {
    setDraft(next);
    props.onCommit?.(next);
  };

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
      refCount: typeof metadata.refCount === "string" ? Number(metadata.refCount) : undefined,
      type: metadata.type ? { kind: metadata.type } : undefined,
    };
  });
  return (
    <div style={{ display: "grid", gap: 4 }}>
      <InlineEditableText
        {...props}
        value={draft}
        className="microflow-inline-expression"
        onCommit={commit}
      />
      {variables?.length ? (
        <ContextVariablePicker
          value={undefined}
          disabled={props.readonly}
          placeholder="插入变量"
          variables={variables}
          insertionMode="append"
          onChange={value => {
            if (!value) {
              return;
            }
            const next = draft ? `${draft} ${value}`.trim() : value;
            commit(next);
          }}
        />
      ) : null}
    </div>
  );
}

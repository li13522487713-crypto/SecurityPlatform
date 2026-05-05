import { ContextVariablePicker, type ContextVariableCandidate } from "./shared/ContextVariablePicker";
import { InlineEditableText } from "./InlineEditableText";

export function InlineExpressionField(props: {
  value: string;
  placeholder?: string;
  readonly?: boolean;
  invalid?: boolean;
  options?: Array<{ label: string; value: string }>;
  onCommit?: (value: string) => void;
}) {
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
  return (
    <div style={{ display: "grid", gap: 4 }}>
      <InlineEditableText {...props} className="microflow-inline-expression" commitOnBlur={false} />
      {variables?.length ? (
        <ContextVariablePicker
          value={props.value}
          disabled={props.readonly}
          placeholder="插入变量"
          variables={variables}
          onChange={value => {
            if (!value) {
              return;
            }
            const normalized = props.value.trim()
              ? `${props.value} ${value}`.trim()
              : value;
            props.onCommit?.(normalized);
          }}
        />
      ) : null}
    </div>
  );
}

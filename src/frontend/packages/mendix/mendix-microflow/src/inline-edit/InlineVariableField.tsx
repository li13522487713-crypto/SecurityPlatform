import { ContextVariablePicker, type ContextVariableCandidate } from "./shared/ContextVariablePicker";
import { InlineEditableText } from "./InlineEditableText";

export function InlineVariableField(props: {
  value: string;
  placeholder?: string;
  readonly?: boolean;
  invalid?: boolean;
  options?: Array<{ label: string; value: string }>;
  onCommit?: (value: string) => void;
}) {
  if (props.options && props.options.length > 0) {
    const variables: ContextVariableCandidate[] = props.options.map(option => {
      const [source, rawMeta] = option.label.includes("::")
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
        source,
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
      <ContextVariablePicker
        value={props.value}
        disabled={props.readonly}
        placeholder={props.placeholder ?? "选择变量"}
        variables={variables}
        onChange={value => props.onCommit?.(value ?? "")}
      />
    );
  }
  return <InlineEditableText {...props} className="microflow-inline-variable" />;
}

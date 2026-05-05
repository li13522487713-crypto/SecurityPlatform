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
      const [source, displayName] = option.label.includes("::")
        ? option.label.split("::", 2)
        : ["context", option.label];
      return {
        name: option.value,
        source,
        sourceNode: displayName,
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

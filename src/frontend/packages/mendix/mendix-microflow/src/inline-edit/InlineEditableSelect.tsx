import { Select } from "@douyinfe/semi-ui";

export function InlineEditableSelect(props: {
  value: string;
  readonly?: boolean;
  options?: Array<{ label: string; value: string }>;
  onCommit?: (value: string) => void;
}) {
  return (
    <Select
      size="small"
      disabled={props.readonly}
      value={props.value}
      style={{ width: "100%" }}
      optionList={props.options ?? []}
      onChange={value => {
        if (value) {
          props.onCommit?.(String(value));
        }
      }}
    />
  );
}

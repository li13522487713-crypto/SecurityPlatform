import { Input, Typography } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";

const { Text } = Typography;

export function InlineEditableText(props: {
  value: string;
  placeholder?: string;
  readonly?: boolean;
  invalid?: boolean;
  className?: string;
  commitOnBlur?: boolean;
  onCommit?: (value: string) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(props.value);

  useEffect(() => {
    setDraft(props.value);
  }, [props.value]);

  if (!editing || props.readonly) {
    return (
      <button
        type="button"
        className={["microflow-inline-field", props.className, props.invalid ? "is-invalid" : ""].filter(Boolean).join(" ")}
        onClick={() => {
          if (!props.readonly) {
            setEditing(true);
          }
        }}
      >
        {props.value || <Text type="tertiary">{props.placeholder ?? "点击编辑"}</Text>}
      </button>
    );
  }

  return (
    <Input
      autoFocus
      size="small"
      value={draft}
      className="microflow-inline-field is-editing"
      onChange={setDraft}
      onKeyDown={event => {
        if (event.key === "Enter") {
          setEditing(false);
          props.onCommit?.(draft);
        }
        if (event.key === "Escape") {
          setEditing(false);
          setDraft(props.value);
        }
      }}
      onBlur={() => {
        setEditing(false);
        if (props.commitOnBlur !== false) {
          props.onCommit?.(draft);
        }
      }}
    />
  );
}

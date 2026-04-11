import { Select } from "antd";

interface TagInputProps {
  value: unknown;
  onChange: (next: string[]) => void;
}

function toTags(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value
      .map((item) => (typeof item === "string" ? item : String(item ?? "")))
      .map((item) => item.trim())
      .filter((item) => item.length > 0);
  }
  if (typeof value === "string") {
    return value
      .split(/[,\r\n;]/)
      .map((item) => item.trim())
      .filter((item) => item.length > 0);
  }
  return [];
}

export function TagInput(props: TagInputProps) {
  return (
    <Select
      mode="tags"
      style={{ width: "100%" }}
      value={toTags(props.value)}
      tokenSeparators={[",", ";"]}
      placeholder="输入后回车添加"
      onChange={(next) => props.onChange(next)}
    />
  );
}


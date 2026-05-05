import { InlineExpressionField } from "./InlineExpressionField";

export function InlineAssignmentEditor(props: {
  value: string;
  placeholder?: string;
  readonly?: boolean;
  invalid?: boolean;
  options?: Array<{ label: string; value: string }>;
  onCommit?: (value: string) => void;
}) {
  return <InlineExpressionField {...props} />;
}

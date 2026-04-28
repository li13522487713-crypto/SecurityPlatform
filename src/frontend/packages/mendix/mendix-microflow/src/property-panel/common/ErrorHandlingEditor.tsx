import { Select, Typography } from "@douyinfe/semi-ui";
import type { MicroflowActionKind, MicroflowErrorHandlingType, MicroflowObjectKind, MicroflowValidationIssue } from "../../schema";
import { FieldError } from "./FieldError";

const { Text } = Typography;

const labels: Record<MicroflowErrorHandlingType, string> = {
  rollback: "Rollback",
  customWithRollback: "Custom with rollback",
  customWithoutRollback: "Custom without rollback",
  continue: "Continue",
};

function reasonFor(type: MicroflowErrorHandlingType, actionKind?: MicroflowActionKind, objectKind?: MicroflowObjectKind): string | undefined {
  if (type === "continue" && actionKind && actionKind !== "callMicroflow") {
    return "Continue is reserved for call microflow or registry-enabled actions.";
  }
  if (type === "continue" && objectKind && objectKind !== "loopedActivity") {
    return "Continue is only meaningful for loop-like control flow.";
  }
  if (type === "rollback") {
    return "Terminates the microflow and rolls back the transaction.";
  }
  if (type === "customWithRollback" || type === "customWithoutRollback") {
    return "Requires an error handler flow from this node.";
  }
  return undefined;
}

export function ErrorHandlingEditor({
  value,
  onChange,
  supportedTypes,
  actionKind,
  objectKind,
  fieldPath,
  issues,
  readonly,
}: {
  value: MicroflowErrorHandlingType;
  onChange: (next: MicroflowErrorHandlingType) => void;
  supportedTypes: MicroflowErrorHandlingType[];
  actionKind?: MicroflowActionKind;
  objectKind?: MicroflowObjectKind;
  fieldPath: string;
  issues: MicroflowValidationIssue[];
  readonly?: boolean;
}) {
  const reason = reasonFor(value, actionKind, objectKind);
  return (
    <div style={{ display: "grid", gap: 6, width: "100%" }} data-field-path={fieldPath}>
      <Select
        value={value}
        disabled={readonly}
        style={{ width: "100%" }}
        onChange={next => onChange(String(next) as MicroflowErrorHandlingType)}
        optionList={(["rollback", "customWithRollback", "customWithoutRollback", "continue"] as MicroflowErrorHandlingType[]).map(type => ({
          label: labels[type],
          value: type,
          disabled: !supportedTypes.includes(type),
        }))}
      />
      {reason ? <Text type="tertiary" size="small">{reason}</Text> : null}
      {actionKind === "restCall" ? <Text type="tertiary" size="small">$latestHttpResponse is available inside REST error handlers.</Text> : null}
      <FieldError issues={issues} />
    </div>
  );
}

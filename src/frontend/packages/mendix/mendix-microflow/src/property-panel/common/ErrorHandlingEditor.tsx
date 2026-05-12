import { Select, Typography } from "@douyinfe/semi-ui";
import type { MicroflowActionKind, MicroflowErrorHandlingType, MicroflowObjectKind, MicroflowValidationIssue } from "../../schema";
import { FieldError } from "./FieldError";

const { Text } = Typography;

export const OFFICIAL_ERROR_HANDLING_LABELS: Record<MicroflowErrorHandlingType, string> = {
  rollback: "Rollback",
  customWithRollback: "Custom with Rollback",
  customWithoutRollback: "Custom without Rollback",
  continue: "Continue",
};

function reasonFor(type: MicroflowErrorHandlingType, actionKind?: MicroflowActionKind, objectKind?: MicroflowObjectKind): string | undefined {
  if (type === "rollback") {
    return "发生错误后终止执行，并回滚当前事务。";
  }
  if (type === "customWithRollback") {
    return "发生错误后进入自定义错误路径；原事务回滚，错误路径在新事务中执行。";
  }
  if (type === "customWithoutRollback") {
    return "发生错误后进入自定义错误路径；当前事务不回滚，错误路径继续执行。";
  }
  if (type === "continue") {
    if (objectKind === "loopedActivity") {
      return "发生错误后忽略当前错误并继续 Loop 后续执行；可通过 $latestError 检查错误详情。";
    }
    if (actionKind === "restCall") {
      return "发生错误后忽略当前错误并继续执行后续节点；可通过 $latestError 检查错误详情。";
    }
    return "发生错误后忽略当前错误并继续执行后续节点；可通过 $latestError 检查错误详情。";
  }
  return undefined;
}

export function supportedErrorHandlingTypesForAction(actionKind?: MicroflowActionKind): MicroflowErrorHandlingType[] {
  if (!actionKind) {
    return ["rollback", "customWithRollback", "customWithoutRollback"];
  }
  if (actionKind === "logMessage") {
    return ["rollback"];
  }
  if (actionKind === "callMicroflow" || actionKind === "restCall") {
    return ["rollback", "customWithRollback", "customWithoutRollback", "continue"];
  }
  return ["rollback", "customWithRollback", "customWithoutRollback"];
}

export function supportedErrorHandlingTypesForObject(objectKind?: MicroflowObjectKind): MicroflowErrorHandlingType[] {
  if (objectKind === "loopedActivity" || objectKind === "exclusiveSplit" || objectKind === "inheritanceSplit") {
    return ["rollback", "customWithRollback", "customWithoutRollback", "continue"];
  }
  return ["rollback", "customWithRollback", "customWithoutRollback"];
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
          label: OFFICIAL_ERROR_HANDLING_LABELS[type],
          value: type,
          disabled: !supportedTypes.includes(type),
        }))}
      />
      {reason ? <Text type="tertiary" size="small">{reason}</Text> : null}
      {actionKind === "restCall" ? <Text type="tertiary" size="small">REST 错误路径中还可读取 $latestHttpResponse。</Text> : null}
      {actionKind === "webServiceCall" ? <Text type="tertiary" size="small">Web Service 错误路径中还可读取 $latestSoapFault。</Text> : null}
      <FieldError issues={issues} />
    </div>
  );
}

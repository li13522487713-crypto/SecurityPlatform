import { type ReactNode, useState } from "react";
import { Button, Space, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconChevronDown, IconChevronRight, IconHelpCircle } from "@douyinfe/semi-icons";
import type { MicroflowValidationIssue } from "../../schema";
import { FieldError } from "./FieldError";
import { RequiredMark } from "./RequiredMark";

const { Text } = Typography;

export function FieldRow({
  label,
  fieldPath,
  required,
  tooltip,
  issues,
  collapsible,
  defaultCollapsed = false,
  children,
}: {
  label: string;
  fieldPath?: string;
  required?: boolean;
  tooltip?: string;
  issues?: MicroflowValidationIssue[];
  collapsible?: boolean;
  defaultCollapsed?: boolean;
  children: ReactNode;
}) {
  const [collapsed, setCollapsed] = useState(defaultCollapsed);
  return (
    <div data-field-path={fieldPath} style={{ display: "grid", gap: 6, width: "100%" }}>
      <Space spacing={4}>
        {collapsible ? (
          <Button
            icon={collapsed ? <IconChevronRight /> : <IconChevronDown />}
            theme="borderless"
            size="small"
            onClick={() => setCollapsed(value => !value)}
          />
        ) : null}
        <Text size="small" strong>{label}</Text>
        {required ? <RequiredMark /> : null}
        {tooltip ? (
          <Tooltip content={tooltip}>
            <IconHelpCircle style={{ color: "var(--semi-color-text-2)" }} />
          </Tooltip>
        ) : null}
      </Space>
      {collapsed ? null : children}
      <FieldError issues={issues} />
    </div>
  );
}

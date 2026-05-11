import { Button, Card, List } from "@douyinfe/semi-ui";

export interface DebugVariablesPanelItem {
  name: string;
  valuePreview: string;
}

export interface DebugVariablesPanelProps {
  title: string;
  variables: DebugVariablesPanelItem[];
  activeVariableName?: string;
  onSelectVariable?: (variableName: string) => void;
}

function normalizeVariableName(name: string | undefined): string {
  const trimmed = String(name ?? "").trim();
  if (!trimmed) {
    return "";
  }
  return trimmed.startsWith("$") ? trimmed : `$${trimmed}`;
}

export function DebugVariablesPanel({
  title,
  variables,
  activeVariableName,
  onSelectVariable,
}: DebugVariablesPanelProps) {
  return (
    <Card title={title}>
      <List
        dataSource={variables}
        renderItem={item => (
          <List.Item>
            <Button
              theme="borderless"
              type={normalizeVariableName(activeVariableName) === normalizeVariableName(item.name) ? "primary" : "tertiary"}
              style={{ paddingInline: 0 }}
              onClick={() => onSelectVariable?.(item.name)}
              data-testid={`microflow-debug-variable-${normalizeVariableName(item.name).replace(/[^a-zA-Z0-9_-]/g, "-")}`}
            >
              {item.name}: {item.valuePreview}
            </Button>
          </List.Item>
        )}
      />
    </Card>
  );
}

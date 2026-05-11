import { useEffect, useMemo, useRef, useState } from "react";
import { Button, Card, List, Space, Tag, Typography } from "@douyinfe/semi-ui";

export interface DebugVariablesPanelItem {
  name: string;
  valuePreview: string;
  type?: string;
  scope?: string;
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

function variableTypeLabelColor(type: string | undefined): { label: string; color: "blue" | "green" | "orange" | "grey" | "purple" | "red" } {
  const normalized = String(type ?? "").toLowerCase();
  if (normalized.includes("object")) {
    return { label: "Object", color: "blue" };
  }
  if (normalized.includes("list") || normalized.includes("array")) {
    return { label: "List", color: "green" };
  }
  if (normalized.includes("decimal") || normalized.includes("int") || normalized.includes("number")) {
    return { label: "Decimal", color: "orange" };
  }
  if (normalized.includes("boolean") || normalized.includes("bool")) {
    return { label: "Boolean", color: "purple" };
  }
  if (normalized.includes("error")) {
    return { label: "Error", color: "red" };
  }
  return { label: normalized ? normalized : "String", color: "grey" };
}

function tryParsePreview(valuePreview: string): unknown {
  const text = String(valuePreview ?? "").trim();
  if (!text) {
    return undefined;
  }
  if (!((text.startsWith("{") && text.endsWith("}")) || (text.startsWith("[") && text.endsWith("]")))) {
    return undefined;
  }
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

function isLatestErrorVariable(name: string): boolean {
  return normalizeVariableName(name).toLowerCase() === "$latesterror";
}

function variableTestId(name: string): string {
  return normalizeVariableName(name).replace(/[^a-zA-Z0-9_-]/g, "-");
}

export function DebugVariablesPanel({
  title,
  variables,
  activeVariableName,
  onSelectVariable,
}: DebugVariablesPanelProps) {
  const [expandedVariables, setExpandedVariables] = useState<Record<string, boolean>>({});
  const previousValueByNameRef = useRef<Record<string, string>>({});

  const orderedVariables = useMemo(() => {
    const next = [...variables];
    next.sort((a, b) => {
      const aLatestError = isLatestErrorVariable(a.name);
      const bLatestError = isLatestErrorVariable(b.name);
      if (aLatestError && !bLatestError) {
        return -1;
      }
      if (!aLatestError && bLatestError) {
        return 1;
      }
      return normalizeVariableName(a.name).localeCompare(normalizeVariableName(b.name));
    });
    return next;
  }, [variables]);

  const changedVariables = useMemo(() => {
    const changed = new Set<string>();
    for (const item of orderedVariables) {
      const name = normalizeVariableName(item.name);
      if (!name) {
        continue;
      }
      if (previousValueByNameRef.current[name] != null && previousValueByNameRef.current[name] !== item.valuePreview) {
        changed.add(name);
      }
    }
    return changed;
  }, [orderedVariables]);

  useEffect(() => {
    const next: Record<string, string> = {};
    for (const item of orderedVariables) {
      const name = normalizeVariableName(item.name);
      if (!name) {
        continue;
      }
      next[name] = item.valuePreview;
    }
    previousValueByNameRef.current = next;
  }, [orderedVariables]);

  return (
    <Card title={title}>
      <List
        dataSource={orderedVariables}
        renderItem={item => {
          const normalizedName = normalizeVariableName(item.name);
          const active = normalizeVariableName(activeVariableName) === normalizedName;
          const changed = changedVariables.has(normalizedName);
          const parsed = tryParsePreview(item.valuePreview);
          const expanded = expandedVariables[normalizedName] === true;
          const typeInfo = variableTypeLabelColor(item.type);
          const canExpand = parsed != null && (Array.isArray(parsed) || typeof parsed === "object");
          const listSize = Array.isArray(parsed) ? parsed.length : undefined;
          const objectEntries = parsed && !Array.isArray(parsed) && typeof parsed === "object" ? Object.entries(parsed as Record<string, unknown>) : undefined;
          return (
            <List.Item>
              <div
                data-testid={`microflow-debug-variable-row-${variableTestId(item.name)}`}
                style={{
                  width: "100%",
                  borderRadius: 6,
                  padding: 6,
                  background: changed ? "rgba(96, 165, 250, 0.08)" : "transparent",
                  border: changed ? "1px solid rgba(96, 165, 250, 0.35)" : "1px solid transparent",
                }}
              >
                <Space vertical align="start" style={{ width: "100%" }}>
                  <Space wrap align="center" style={{ width: "100%", justifyContent: "space-between" }}>
                    <Space wrap align="center">
                      <Tag color={typeInfo.color} size="small">{typeInfo.label}</Tag>
                      {item.scope ? <Tag color="grey" size="small">{item.scope}</Tag> : null}
                      {changed ? <Tag color="blue" size="small">●</Tag> : null}
                    </Space>
                    {canExpand ? (
                      <Button
                        theme="borderless"
                        size="small"
                        onClick={() => setExpandedVariables(current => ({ ...current, [normalizedName]: !expanded }))}
                        data-testid={`microflow-debug-variable-expand-${variableTestId(item.name)}`}
                      >
                        {expanded ? "收起" : "展开"}
                      </Button>
                    ) : null}
                  </Space>
                  <Button
                    theme="borderless"
                    type={active ? "primary" : "tertiary"}
                    style={{ paddingInline: 0, width: "100%", justifyContent: "flex-start", textAlign: "left" }}
                    onClick={() => onSelectVariable?.(item.name)}
                    data-testid={`microflow-debug-variable-${variableTestId(item.name)}`}
                  >
                    {item.name}: {item.valuePreview}
                  </Button>
                  {expanded && Array.isArray(parsed) ? (
                    <div data-testid={`microflow-debug-variable-expanded-${variableTestId(item.name)}`} style={{ width: "100%", paddingLeft: 8 }}>
                      <Typography.Text type="tertiary" size="small">List[{listSize ?? 0}]</Typography.Text>
                      {(parsed as unknown[]).slice(0, 10).map((entry, index) => (
                        <div key={`${normalizedName}-${index}`} style={{ marginTop: 2 }}>
                          <Typography.Text size="small">[{index}] {String(entry)}</Typography.Text>
                        </div>
                      ))}
                      {(listSize ?? 0) > 10 ? (
                        <Typography.Text type="tertiary" size="small">... 共 {listSize} 项</Typography.Text>
                      ) : null}
                    </div>
                  ) : null}
                  {expanded && objectEntries ? (
                    <div data-testid={`microflow-debug-variable-expanded-${variableTestId(item.name)}`} style={{ width: "100%", paddingLeft: 8 }}>
                      {objectEntries.map(([key, value]) => (
                        <div key={`${normalizedName}-${key}`} style={{ marginTop: 2 }}>
                          <Typography.Text size="small">/{key} {String(value)}</Typography.Text>
                        </div>
                      ))}
                    </div>
                  ) : null}
                </Space>
              </div>
            </List.Item>
          );
        }}
      />
    </Card>
  );
}

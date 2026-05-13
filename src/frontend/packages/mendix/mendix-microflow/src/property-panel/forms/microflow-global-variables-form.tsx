import { useEffect, useMemo, useState } from "react";
import { Button, Input, Select, Space, Tag, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowDataType, MicroflowGlobalVariable } from "../../schema/types";
import { createStableId, getGlobalVariableNameWarning, getMicroflowGlobalVariables, removeMicroflowGlobalVariable, upsertMicroflowGlobalVariable } from "../../schema/utils";
import { Field } from "../panel-shared";
import type { MicroflowPropertyPanelProps } from "../types";

const { Text, Title } = Typography;

const dataTypeOptions = [
  { label: "string", value: "string" },
  { label: "integer", value: "integer" },
  { label: "long", value: "long" },
  { label: "decimal", value: "decimal" },
  { label: "boolean", value: "boolean" },
  { label: "dateTime", value: "dateTime" },
  { label: "json", value: "json" },
];

function buildDataType(kind: string): MicroflowDataType {
  switch (kind) {
    case "boolean":
    case "integer":
    case "long":
    case "decimal":
    case "string":
    case "dateTime":
    case "json":
      return { kind };
    default:
      return { kind: "string" };
  }
}

export function MicroflowGlobalVariablesForm(props: Pick<MicroflowPropertyPanelProps, "schema" | "onSchemaChange" | "readonly">) {
  const { schema, onSchemaChange, readonly } = props;
  const globalVariables = getMicroflowGlobalVariables(schema);
  const [selectedId, setSelectedId] = useState<string>(() => globalVariables[0]?.id ?? "");

  useEffect(() => {
    if (selectedId && !globalVariables.some(v => v.id === selectedId)) {
      setSelectedId(globalVariables[0]?.id ?? "");
    }
  }, [globalVariables, selectedId]);

  const selected = useMemo(() => globalVariables.find(v => v.id === selectedId), [globalVariables, selectedId]);

  const nameWarning = selected ? getGlobalVariableNameWarning(schema, selected.id, selected.name) : undefined;

  const addVariable = () => {
    if (!onSchemaChange) return;
    const id = createStableId("gvar");
    const newVar: MicroflowGlobalVariable = {
      id,
      name: `GlobalVar${globalVariables.length + 1}`,
      dataType: { kind: "string" },
    };
    onSchemaChange(upsertMicroflowGlobalVariable(schema, newVar), "addGlobalVariable");
    setSelectedId(id);
  };

  const removeSelected = () => {
    if (!onSchemaChange || !selected) return;
    onSchemaChange(removeMicroflowGlobalVariable(schema, selected.id), "removeGlobalVariable");
  };

  const patchSelected = (patch: Partial<MicroflowGlobalVariable>) => {
    if (!onSchemaChange || !selected) return;
    onSchemaChange(upsertMicroflowGlobalVariable(schema, { ...selected, ...patch }), "updateGlobalVariable");
  };

  return (
    <div style={{ padding: "14px 14px 0" }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
        <Title heading={6} style={{ margin: 0 }}>微流全局变量</Title>
        <Button size="small" disabled={readonly || !onSchemaChange} onClick={addVariable}>+ 添加变量</Button>
      </div>
      <Text size="small" type="tertiary" style={{ display: "block", marginBottom: 10 }}>
        全局变量在微流启动时初始化，任意节点均可读写，无需连接到流程图中。
      </Text>

      {globalVariables.length === 0 ? (
        <Text size="small" type="tertiary">暂无全局变量。点击「添加变量」创建。</Text>
      ) : (
        <Space vertical align="start" spacing={4} style={{ width: "100%", marginBottom: 8 }}>
          {globalVariables.map(variable => (
            <Tag
              key={variable.id}
              color={variable.id === selectedId ? "blue" : "grey"}
              style={{ cursor: "pointer", userSelect: "none" }}
              onClick={() => setSelectedId(variable.id)}
            >
              {variable.name || "(未命名)"} [{variable.dataType.kind}]
            </Tag>
          ))}
        </Space>
      )}

      {selected ? (
        <div style={{ borderTop: "1px solid var(--semi-color-border, #e5e6eb)", paddingTop: 10, display: "grid", gap: 10 }}>
          <Field label="变量名称">
            <Input
              value={selected.name}
              disabled={readonly}
              placeholder="VariableName"
              onChange={name => patchSelected({ name })}
            />
            {nameWarning ? <Text size="small" type="danger">{nameWarning}</Text> : null}
          </Field>
          <Field label="数据类型">
            <Select
              value={selected.dataType.kind}
              disabled={readonly}
              style={{ width: "100%" }}
              optionList={dataTypeOptions}
              onChange={kind => patchSelected({ dataType: buildDataType(String(kind)) })}
            />
          </Field>
          <Field label="初始值（表达式）">
            <Input
              value={selected.initialValue?.raw ?? ""}
              disabled={readonly}
              placeholder="例: 'defaultValue' 或 0"
              onChange={raw => patchSelected({ initialValue: raw ? { raw, references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] } : undefined })}
            />
            <Text size="small" type="tertiary">留空表示使用类型默认值。</Text>
          </Field>
          <Field label="说明">
            <TextArea
              value={selected.description ?? ""}
              disabled={readonly}
              autosize
              placeholder="变量用途说明"
              onChange={description => patchSelected({ description })}
            />
          </Field>
          <Button
            size="small"
            type="danger"
            disabled={readonly || !onSchemaChange}
            onClick={removeSelected}
            style={{ alignSelf: "flex-start" }}
          >
            删除变量
          </Button>
        </div>
      ) : null}
    </div>
  );
}

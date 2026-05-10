import { useState } from "react";
import { Button, Tag, Tabs, Typography } from "@douyinfe/semi-ui";
import { formatMendixMicroflowTemplate, getMendixMicroflowCopy } from "../i18n/copy";
import type { MicroflowRuntimeValueGroup, MicroflowRuntimeValueViewModel } from "../debug/runtime-value-view-model";
import type { MicroflowNodeRuntimeInlineState } from "../flowgram/FlowGramMicroflowTypes";

const { Text } = Typography;

function statusLabel(runtime: MicroflowNodeRuntimeInlineState): string {
  if (runtime.running) return "running";
  if (runtime.success) return "success";
  if (runtime.failed) return "failed";
  if (runtime.skipped) return "skipped";
  return "visited";
}

function jsonBlock(value: string) {
  return <pre className="microflow-runtime-inspector__json">{value}</pre>;
}

function RuntimeFieldTable(props: { value: MicroflowRuntimeValueViewModel }) {
  const copy = getMendixMicroflowCopy().runtimeInspector;
  const rows = props.value.fields.length > 0
    ? props.value.fields
    : [{ path: "value", type: props.value.type, value: props.value.valuePreview }];
  return (
    <table className="microflow-runtime-inspector__table">
      <thead>
        <tr>
          <th>{copy.field}</th>
          <th>{copy.type}</th>
          <th>{copy.value}</th>
        </tr>
      </thead>
      <tbody>
        {rows.map(row => (
          <tr key={row.path}>
            <td title={row.path}>{row.path}</td>
            <td title={row.type}>{row.type}</td>
            <td title={row.value}>{row.value}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function RuntimeListTable(props: { value: MicroflowRuntimeValueViewModel }) {
  const copy = getMendixMicroflowCopy().runtimeInspector;
  const [mode, setMode] = useState<"table" | "json">("table");
  const list = props.value.list;
  if (!list) {
    return <RuntimeFieldTable value={props.value} />;
  }
  return (
    <div className="microflow-runtime-inspector__list">
      <div className="microflow-runtime-inspector__list-toolbar">
        <Text type="tertiary" size="small">
          {formatMendixMicroflowTemplate(copy.rowsFields, { rows: list.rowCount, fields: list.fieldCount })}
        </Text>
        <span className="microflow-runtime-inspector__mode-buttons">
          <Button size="small" type={mode === "table" ? "primary" : "tertiary"} onClick={() => setMode("table")}>{copy.tableMode}</Button>
          <Button size="small" type={mode === "json" ? "primary" : "tertiary"} onClick={() => setMode("json")}>{copy.jsonMode}</Button>
        </span>
      </div>
      {mode === "json" ? jsonBlock(list.json) : (
        <table className="microflow-runtime-inspector__table">
          <thead>
            <tr>
              <th>#</th>
              {list.columns.map(column => <th key={column}>{column}</th>)}
            </tr>
          </thead>
          <tbody>
            {list.rows.map(row => (
              <tr key={row["#"]}>
                <td>{row["#"]}</td>
                {list.columns.map(column => <td key={column} title={row[column]}>{row[column]}</td>)}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

function RuntimeValueCard(props: { value: MicroflowRuntimeValueViewModel }) {
  const copy = getMendixMicroflowCopy().runtimeInspector;
  return (
    <div className="microflow-runtime-inspector__value-card">
      <div className="microflow-runtime-inspector__value-head">
        <Text strong>{props.value.name}</Text>
        <span className="microflow-runtime-inspector__tags">
          <Tag size="small">{props.value.type}</Tag>
          {props.value.source ? <Tag size="small">{copy.source}: {props.value.source}</Tag> : null}
        </span>
      </div>
      {props.value.kind === "list" ? <RuntimeListTable value={props.value} /> : <RuntimeFieldTable value={props.value} />}
    </div>
  );
}

function RuntimeGroup(props: { group?: MicroflowRuntimeValueGroup; emptyLabel: string }) {
  const values = props.group?.values ?? [];
  if (values.length === 0) {
    return <Text type="tertiary" size="small">{props.emptyLabel}</Text>;
  }
  return (
    <div className="microflow-runtime-inspector__group">
      {values.map(value => <RuntimeValueCard key={value.name} value={value} />)}
    </div>
  );
}

export function InlineRuntimePreview(props: { runtime?: MicroflowNodeRuntimeInlineState }) {
  const runtime = props.runtime;
  if (!runtime) {
    return null;
  }
  const copy = getMendixMicroflowCopy().runtimeInspector;
  const selectedText = runtime.selectedBranchLabel ? `${copy.selected}: ${runtime.selectedBranchLabel}` : "";
  return (
    <div className="microflow-runtime-inline">
      <div className="microflow-runtime-inspector__summary">
        <Text strong>{copy.title}</Text>
        {runtime.running ? <Tag color="blue">running</Tag> : null}
        {runtime.success ? <Tag color="green">success</Tag> : null}
        {runtime.failed ? <Tag color="red">failed</Tag> : null}
        {runtime.skipped ? <Tag color="grey">skipped</Tag> : null}
        {typeof runtime.durationMs === "number" ? <Tag>{copy.duration}: {runtime.durationMs}ms</Tag> : null}
        <Tag>{copy.inputs}: {runtime.inputCount ?? 0}</Tag>
        <Tag>{copy.outputs}: {runtime.outputCount ?? 0}</Tag>
      </div>
      <Text size="small" type="tertiary">
        {copy.status}: {statusLabel(runtime)}
        {selectedText ? ` · ${selectedText}` : ""}
      </Text>
      <Tabs type="card" size="small" className="microflow-runtime-inspector__tabs">
        <Tabs.TabPane tab={copy.outputTab} itemKey="output">
          <RuntimeGroup group={runtime.outputGroup} emptyLabel={copy.noOutput} />
        </Tabs.TabPane>
        <Tabs.TabPane tab={copy.inputTab} itemKey="input">
          <RuntimeGroup group={runtime.inputGroup} emptyLabel={copy.noInput} />
        </Tabs.TabPane>
        <Tabs.TabPane tab={copy.variablesTab} itemKey="variables">
          <RuntimeGroup group={runtime.variableGroup} emptyLabel={copy.noVariables} />
        </Tabs.TabPane>
        <Tabs.TabPane tab={copy.jsonTab} itemKey="json">
          {jsonBlock(runtime.rawTraceJson ?? "{}")}
        </Tabs.TabPane>
      </Tabs>
    </div>
  );
}

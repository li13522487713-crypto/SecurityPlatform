import type { MicroflowQueryExternalDatabaseAction } from "../schema/types";
import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

function inferSqlMode(sql: string): "query" | "mutation" | "unknown" {
  const trimmed = (sql ?? "").trim().toLowerCase();
  if (trimmed.startsWith("select") || trimmed.startsWith("with") || trimmed.startsWith("show")) {
    return "query";
  }
  if (trimmed.startsWith("insert") || trimmed.startsWith("update") || trimmed.startsWith("delete") || trimmed.startsWith("merge")) {
    return "mutation";
  }
  return "unknown";
}

export function deriveDatabaseNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const variableNameOptions = buildNodeInlineVariableOptions({
    schema: input.schema,
    node: input.node,
    runtimeFrame: input.runtimeFrame,
    mode: "name",
  });

  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const action = (data.action ?? {}) as MicroflowQueryExternalDatabaseAction | Record<string, unknown>;

  const databaseAction = action as MicroflowQueryExternalDatabaseAction;
  const sql = databaseAction.sql ?? "";
  const outputVar = databaseAction.output?.variableName ?? "";
  const sourceName = (databaseAction as unknown as Record<string, string>)._sourceName ?? databaseAction.databaseSourceId ?? "";
  const driverCode = (databaseAction as unknown as Record<string, string>).driverCode ?? "";
  const sqlMode = inferSqlMode(sql);
  const hasSource = Boolean(databaseAction.databaseSourceId);
  const hasSql = Boolean(sql);
  const isConfigured = hasSource && hasSql && Boolean(outputVar);

  const modeLabel = sqlMode === "query" ? "查询" : sqlMode === "mutation" ? "写入" : "SQL";
  const sourceLabel = sourceName ? `${sourceName}${driverCode ? ` (${driverCode})` : ""}` : "未选择数据源";
  const sqlPreview = sql.length > 60 ? `${sql.slice(0, 60)}...` : sql;

  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "source", value: `src: ${sourceLabel}`, kind: "input" },
    hasSql
      ? { id: "sql", value: `${modeLabel}: ${sqlPreview || "-"}`, kind: "http" }
      : { id: "sql", value: "sql: 未配置", kind: "error" },
    { id: "out", value: `out: ${outputVar ? `$.${outputVar}` : "-"}`, kind: "output" },
    ...(base.runtime?.outputPreview ? [{ id: "run", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
  ];

  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: [
      {
        id: "db-core",
        title: "数据库",
        kind: "http",
        maxVisibleRows: 4,
        fields: [
          {
            id: "source",
            label: "src",
            value: databaseAction.databaseSourceId ?? "",
            fieldPath: "data.action.databaseSourceId",
            editType: "text",
            required: true,
          },
          {
            id: "sql",
            label: "sql",
            value: sql,
            fieldPath: "data.action.sql",
            editType: "text",
            required: true,
          },
          {
            id: "out",
            label: "out",
            value: outputVar,
            fieldPath: "data.action.output.variableName",
            editType: "variable",
            required: true,
            options: variableNameOptions,
          },
          {
            id: "schema",
            label: "schema",
            value: databaseAction.schemaName ?? "",
            fieldPath: "data.action.schemaName",
            editType: "text",
          },
        ],
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}

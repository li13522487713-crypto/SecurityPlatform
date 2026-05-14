import { useCallback, useEffect, useRef, useState } from "react";
import { Button, Input, Select, Space, Spin, Switch, Tag, TextArea, Toast, Typography } from "@douyinfe/semi-ui";
import { IconExternalOpen, IconRefresh, IconSearch } from "@douyinfe/semi-icons";
import { useMicroflowMetadataContext } from "../../metadata/metadata-provider";
import type { MicroflowDatabaseSourceSummary, MicroflowDatabaseSchemaStructure } from "../../metadata/metadata-adapter";
import { FieldRow } from "../controls";
import type { MicroflowNodeFormProps } from "../types";
import type { MicroflowRegistryActivityNode } from "../../schema";
import { scanMicroflowSqlPlaceholders } from "../../sql-editor/microflow-sql-decorations";

type ActivityFormProps = MicroflowNodeFormProps<MicroflowRegistryActivityNode>;

const { Text, Title } = Typography;

function updateActivityConfig(props: ActivityFormProps, patch: Record<string, unknown>) {
  props.onPatch({ config: patch });
}

function driverCodeIcon(driverCode: string): string {
  const map: Record<string, string> = {
    mysql: "MySQL",
    postgresql: "PostgreSQL",
    mssql: "SQL Server",
    oracle: "Oracle",
    sqlite: "SQLite",
    mariadb: "MariaDB",
  };
  return map[driverCode?.toLowerCase()] ?? driverCode ?? "DB";
}

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

/** 连接 Tab */
function ConnectionTab({ props }: { props: ActivityFormProps }) {
  const { adapter, workspaceId } = useMicroflowMetadataContext();
  const [sources, setSources] = useState<MicroflowDatabaseSourceSummary[]>([]);
  const [loadingSources, setLoadingSources] = useState(false);
  const [schemaStructure, setSchemaStructure] = useState<MicroflowDatabaseSchemaStructure | null>(null);
  const [loadingSchema, setLoadingSchema] = useState(false);
  const config = props.node.config;
  const hasAdapter = typeof adapter?.getDatabaseSources === "function";

  const loadSources = useCallback(async () => {
    if (!hasAdapter || !adapter?.getDatabaseSources) return;
    setLoadingSources(true);
    try {
      const result = await adapter.getDatabaseSources({ workspaceId });
      setSources(result);
    } catch {
      Toast.error("加载数据库连接失败");
    } finally {
      setLoadingSources(false);
    }
  }, [adapter, hasAdapter, workspaceId]);

  useEffect(() => {
    void loadSources();
  }, [loadSources]);

  const loadSchema = useCallback(async (sourceId: string, schemaName?: string) => {
    if (!adapter?.getDatabaseSchemaStructure) return;
    setLoadingSchema(true);
    try {
      const result = await adapter.getDatabaseSchemaStructure(sourceId, schemaName);
      setSchemaStructure(result);
    } catch {
      Toast.error("加载表结构失败");
    } finally {
      setLoadingSchema(false);
    }
  }, [adapter]);

  const handleSourceChange = useCallback((sourceId: string) => {
    const source = sources.find(s => s.id === sourceId);
    updateActivityConfig(props, {
      databaseSourceId: sourceId,
      _sourceName: source?.name ?? "",
      driverCode: source?.driverCode ?? "",
      schemaName: source?.defaultSchemaName ?? "",
    });
    if (sourceId && source?.defaultSchemaName) {
      void loadSchema(sourceId, source.defaultSchemaName);
    }
  }, [props, sources, loadSchema]);

  const sourceOptions = sources.map(s => ({
    label: (
      <Space spacing={6}>
        <Tag size="small" color="blue">{driverCodeIcon(s.driverCode)}</Tag>
        <Text>{s.name}</Text>
        {s.readOnly && <Tag size="small" color="orange">只读</Tag>}
        {s.environment && <Tag size="small">{s.environment}</Tag>}
      </Space>
    ),
    value: s.id,
  }));

  const selectedSource = sources.find(s => s.id === config.databaseSourceId);
  const databaseCenterUrl = workspaceId && config.databaseSourceId
    ? `/space/${workspaceId}/library?tab=database&databaseId=${config.databaseSourceId}`
    : null;

  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <Space align="center" style={{ width: "100%", justifyContent: "space-between" }}>
        <Title heading={6}>数据源连接</Title>
        {databaseCenterUrl && (
          <Button
            icon={<IconExternalOpen />}
            size="small"
            type="tertiary"
            onClick={() => window.open(databaseCenterUrl, "_blank")}
          >
            在数据库中心打开
          </Button>
        )}
      </Space>

      <FieldRow label="数据源" required>
        {hasAdapter ? (
          <Space style={{ width: "100%" }}>
            <Select
              disabled={props.readonly}
              style={{ flex: 1 }}
              loading={loadingSources}
              value={config.databaseSourceId ?? undefined}
              optionList={sourceOptions}
              onChange={v => handleSourceChange(String(v))}
              placeholder="选择数据库连接..."
              showClear
              filter
            />
            <Button
              icon={<IconRefresh />}
              size="small"
              type="tertiary"
              disabled={loadingSources}
              onClick={() => void loadSources()}
            />
          </Space>
        ) : (
          <Input
            readonly={props.readonly}
            value={config.databaseSourceId ?? ""}
            onChange={v => updateActivityConfig(props, { databaseSourceId: v })}
            placeholder="DatabaseCenter 来源 ID"
          />
        )}
      </FieldRow>

      {selectedSource && (
        <Space vertical align="start" spacing={4} style={{ width: "100%" }}>
          <Text type="tertiary" size="small">
            {driverCodeIcon(selectedSource.driverCode)} · {selectedSource.sourceKind}
            {selectedSource.environment && ` · ${selectedSource.environment}`}
          </Text>
        </Space>
      )}

      <FieldRow label="Schema 名称">
        <Input
          readonly={props.readonly}
          value={config.schemaName ?? ""}
          onChange={v => {
            updateActivityConfig(props, { schemaName: v });
            if (config.databaseSourceId) {
              void loadSchema(config.databaseSourceId, v);
            }
          }}
          placeholder="默认 schema"
        />
      </FieldRow>

      {schemaStructure && (
        <Space vertical align="start" spacing={4} style={{ width: "100%" }}>
          {loadingSchema ? (
            <Spin size="small" />
          ) : (
            <>
              <Text strong size="small">已引入表（{schemaStructure.objects.length}）</Text>
              <Space wrap style={{ width: "100%" }}>
                {schemaStructure.objects.slice(0, 12).map(obj => (
                  <Tag key={obj.id} size="small" color="blue">{obj.name}</Tag>
                ))}
                {schemaStructure.objects.length > 12 && (
                  <Tag size="small">+{schemaStructure.objects.length - 12} 更多</Tag>
                )}
              </Space>
            </>
          )}
        </Space>
      )}
    </Space>
  );
}

/** SQL Tab */
function SqlTab({ props }: { props: ActivityFormProps }) {
  const config = props.node.config;
  const mode = inferSqlMode(config.sql ?? "");
  const placeholders = scanMicroflowSqlPlaceholders(config.sql ?? "");
  const localPhs = placeholders.filter(p => p.kind === "local");
  const globalPhs = placeholders.filter(p => p.kind === "global");
  const userPhs = placeholders.filter(p => p.kind === "currentUser");

  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <Space align="center" style={{ width: "100%", justifyContent: "space-between" }}>
        <Title heading={6}>SQL 语句</Title>
        {mode !== "unknown" && (
          <Tag color={mode === "query" ? "blue" : "orange"} size="small">
            {mode === "query" ? "查询" : "写入"}
          </Tag>
        )}
      </Space>

      <FieldRow label="SQL" required>
        <TextArea
          readonly={props.readonly}
          value={config.sql ?? ""}
          rows={10}
          onChange={sql => updateActivityConfig(props, { sql })}
          placeholder={"SELECT *\nFROM orders\nWHERE customer_id = $.customerId\n  AND status = $global.filterStatus"}
          style={{ fontFamily: "monospace", fontSize: 13 }}
        />
      </FieldRow>

      {placeholders.length > 0 && (
        <Space vertical align="start" spacing={4} style={{ width: "100%" }}>
          <Text strong size="small">已识别占位符（{placeholders.length}）</Text>
          <Space wrap>
            {localPhs.map(p => (
              <Tag key={p.raw + p.start} size="small" color="blue">{p.raw}</Tag>
            ))}
            {globalPhs.map(p => (
              <Tag key={p.raw + p.start} size="small" color="orange">{p.raw}</Tag>
            ))}
            {userPhs.map(p => (
              <Tag key={p.raw + p.start} size="small" color="green">{p.raw}</Tag>
            ))}
          </Space>
        </Space>
      )}

      <Space vertical align="start" spacing={4} style={{ width: "100%", padding: "8px", background: "var(--semi-color-fill-0)", borderRadius: 4 }}>
        <Text type="secondary" size="small" strong>占位符说明</Text>
        <Text type="tertiary" size="small">• <code style={{ color: "var(--semi-color-primary)" }}>$.变量名</code> — 当前微流局部变量</Text>
        <Text type="tertiary" size="small">• <code style={{ color: "var(--semi-color-warning)" }}>$global.变量名</code> — 全局变量</Text>
        <Text type="tertiary" size="small">• <code style={{ color: "var(--semi-color-success)" }}>$currentUser.字段名</code> — 当前用户字段（id / name 等）</Text>
        <Text type="tertiary" size="small">后端将自动转为参数化查询，防止 SQL 注入。</Text>
      </Space>
    </Space>
  );
}

/** 输出 Tab */
function OutputTab({ props }: { props: ActivityFormProps }) {
  const config = props.node.config;
  const outputKindOptions = [
    { label: "Object — 首行记录", value: "object" },
    { label: "List — 全部行", value: "list" },
    { label: "Array — 指定列值数组", value: "array" },
  ];

  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <Title heading={6}>输出配置</Title>

      <FieldRow label="输出变量名" required>
        <Input
          readonly={props.readonly}
          value={config.output?.variableName ?? ""}
          onChange={variableName => updateActivityConfig(props, { output: { ...config.output, variableName } })}
          prefix="$."
          placeholder="queryResult"
        />
      </FieldRow>

      <FieldRow label="输出类型">
        <Select
          disabled={props.readonly}
          style={{ width: "100%" }}
          value={config.output?.kind ?? "list"}
          optionList={outputKindOptions}
          onChange={kind => updateActivityConfig(props, { output: { ...config.output, kind: String(kind) } })}
        />
      </FieldRow>

      {config.output?.kind === "array" && (
        <FieldRow label="列名（Array 模式）" required>
          <Input
            readonly={props.readonly}
            value={config.output?.column ?? ""}
            onChange={column => updateActivityConfig(props, { output: { ...config.output, column } })}
            placeholder="id"
          />
        </FieldRow>
      )}

      <FieldRow label="同时赋值全局变量（可选）">
        <Input
          readonly={props.readonly}
          value={config.globalAssignment?.target ?? ""}
          onChange={target => updateActivityConfig(props, { globalAssignment: target ? { target } : undefined })}
          placeholder="$global.queryResult"
        />
      </FieldRow>
    </Space>
  );
}

/** 调试预览 Tab */
function PreviewTab({ props }: { props: ActivityFormProps }) {
  const { adapter } = useMicroflowMetadataContext();
  const config = props.node.config;
  const [result, setResult] = useState<{ columns: Array<{ name: string }>; rows: Record<string, unknown>[]; elapsedMs?: number | null } | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const runPreview = useCallback(async () => {
    if (!config.databaseSourceId || !config.sql) {
      Toast.warning("请先配置数据源和 SQL");
      return;
    }
    if (!adapter?.previewDatabaseSql) {
      Toast.warning("当前环境不支持调试预览");
      return;
    }
    abortRef.current?.abort();
    abortRef.current = new AbortController();
    setLoading(true);
    setError(null);
    try {
      const res = await adapter.previewDatabaseSql(config.databaseSourceId, config.sql, config.schemaName);
      setResult(res);
    } catch (err) {
      setError(err instanceof Error ? err.message : "执行失败");
    } finally {
      setLoading(false);
    }
  }, [adapter, config.databaseSourceId, config.sql, config.schemaName]);

  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <Space align="center" style={{ width: "100%", justifyContent: "space-between" }}>
        <Title heading={6}>调试预览</Title>
        <Button
          icon={<IconSearch />}
          type="primary"
          size="small"
          loading={loading}
          disabled={!config.databaseSourceId || !config.sql}
          onClick={() => void runPreview()}
        >
          执行预览
        </Button>
      </Space>

      <Text type="tertiary" size="small">
        调试预览仅在 Draft 环境下执行，占位符将使用当前调试上下文变量值（如无则替换为空字符串）。
      </Text>

      {error && (
        <Space vertical align="start" style={{ width: "100%", padding: 8, background: "var(--semi-color-danger-light-default)", borderRadius: 4 }}>
          <Text type="danger" size="small">{error}</Text>
        </Space>
      )}

      {result && (
        <Space vertical align="start" spacing={8} style={{ width: "100%", overflowX: "auto" }}>
          <Text size="small" type="tertiary">
            返回 {result.rows.length} 行
            {typeof result.elapsedMs === "number" ? `，耗时 ${result.elapsedMs}ms` : ""}
          </Text>
          <div style={{ overflowX: "auto", width: "100%" }}>
            <table style={{ borderCollapse: "collapse", fontSize: 12, width: "100%" }}>
              <thead>
                <tr>
                  {result.columns.map(col => (
                    <th key={col.name} style={{ border: "1px solid var(--semi-color-border)", padding: "4px 8px", background: "var(--semi-color-fill-1)" }}>
                      {col.name}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {result.rows.slice(0, 50).map((row, i) => (
                  <tr key={i}>
                    {result.columns.map(col => (
                      <td key={col.name} style={{ border: "1px solid var(--semi-color-border)", padding: "4px 8px" }}>
                        {String(row[col.name] ?? "")}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {result.rows.length > 50 && (
            <Text type="tertiary" size="small">仅显示前 50 行</Text>
          )}
        </Space>
      )}
    </Space>
  );
}

/** 高级 Tab */
function AdvancedTab({ props }: { props: ActivityFormProps }) {
  const config = props.node.config;
  const errorModeOptions = [
    { label: "失败并中断（fail）", value: "fail" },
    { label: "继续执行（continue）", value: "continue" },
    { label: "返回空值（empty）", value: "empty" },
  ];

  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <Title heading={6}>高级设置</Title>

      <FieldRow label="错误处理">
        <Select
          disabled={props.readonly}
          style={{ width: "100%" }}
          value={config.advanced?.errorMode ?? "fail"}
          optionList={errorModeOptions}
          onChange={errorMode => updateActivityConfig(props, { advanced: { ...config.advanced, errorMode: String(errorMode) } })}
        />
      </FieldRow>

      <FieldRow label="超时（秒）">
        <input
          type="number"
          readOnly={props.readonly}
          value={config.advanced?.timeoutSeconds ?? 30}
          onChange={e => updateActivityConfig(props, { advanced: { ...config.advanced, timeoutSeconds: Number(e.target.value) } })}
          style={{ width: "100%", padding: "4px 8px", border: "1px solid var(--semi-color-border)", borderRadius: 3 }}
          min={1}
          max={300}
        />
      </FieldRow>

      <FieldRow label="最大返回行数">
        <input
          type="number"
          readOnly={props.readonly}
          value={config.advanced?.maxRows ?? 1000}
          onChange={e => updateActivityConfig(props, { advanced: { ...config.advanced, maxRows: Number(e.target.value) } })}
          style={{ width: "100%", padding: "4px 8px", border: "1px solid var(--semi-color-border)", borderRadius: 3 }}
          min={1}
          max={10000}
        />
      </FieldRow>

      <FieldRow label="使用事务">
        <Switch
          disabled={props.readonly}
          checked={Boolean(config.advanced?.transactional)}
          onChange={transactional => updateActivityConfig(props, { advanced: { ...config.advanced, transactional } })}
        />
      </FieldRow>
    </Space>
  );
}

const DB_TABS = ["连接", "SQL", "输出", "调试预览", "高级"] as const;

export function DatabaseNodeForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as unknown as ActivityFormProps;
  const [activeTab, setActiveTab] = useState<string>("连接");

  return (
    <Space vertical align="start" spacing={0} style={{ width: "100%" }}>
      <Space spacing={0} style={{ width: "100%", borderBottom: "1px solid var(--semi-color-border)", marginBottom: 12 }}>
        {DB_TABS.map(tab => (
          <Button
            key={tab}
            type="tertiary"
            size="small"
            theme={activeTab === tab ? "solid" : "borderless"}
            style={{ borderRadius: 0, borderBottom: activeTab === tab ? "2px solid var(--semi-color-primary)" : "2px solid transparent" }}
            onClick={() => setActiveTab(tab)}
          >
            {tab}
          </Button>
        ))}
      </Space>

      {activeTab === "连接" && <ConnectionTab props={props} />}
      {activeTab === "SQL" && <SqlTab props={props} />}
      {activeTab === "输出" && <OutputTab props={props} />}
      {activeTab === "调试预览" && <PreviewTab props={props} />}
      {activeTab === "高级" && <AdvancedTab props={props} />}
    </Space>
  );
}

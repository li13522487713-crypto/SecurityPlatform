import { useState } from "react";
import { Button, Select, Space, Table, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconPlay, IconRefresh } from "@douyinfe/semi-icons";
import {
  executeDatabaseCenterSql,
  type DatabaseCenterEnvironment,
  type DatabaseCenterSqlResult
} from "../../../services/api-database-center";
import { SqlCodeEditor } from "../database-structure/sql-code-editor";
import type { DatabaseCenterLabels } from "./database-center-labels";
import { formatSql } from "./sql-format-utils";

const { Text } = Typography;

interface SqlEditorPanelProps {
  labels: DatabaseCenterLabels;
  sourceId: string;
  schema?: string;
  environment: DatabaseCenterEnvironment;
  compact?: boolean;
}

export function SqlEditorPanel({ labels, sourceId, schema, environment, compact = false }: SqlEditorPanelProps) {
  const [sql, setSql] = useState("SELECT * FROM demo_table LIMIT 20;");
  const [limit, setLimit] = useState(50);
  const [executing, setExecuting] = useState(false);
  const [result, setResult] = useState<DatabaseCenterSqlResult | null>(null);

  const columns: ColumnProps<Record<string, unknown>>[] = (result?.columns ?? []).map(column => ({
    title: column.name,
    dataIndex: column.name,
    render: (value: unknown) => value == null ? <Text type="tertiary">NULL</Text> : String(value)
  }));

  async function run() {
    if (!sourceId || !sql.trim()) return;
    setExecuting(true);
    try {
      setResult(await executeDatabaseCenterSql({ sourceId, schema, environment, limit, sql }));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "SQL failed");
    } finally {
      setExecuting(false);
    }
  }

  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Space style={{ justifyContent: "space-between", width: "100%" }}>
        <Space>
          <Button icon={<IconPlay />} theme="solid" loading={executing} onClick={() => void run()}>{labels.execute}</Button>
          <Button icon={<IconRefresh />} onClick={() => setSql(formatSql(sql))}>{labels.format}</Button>
        </Space>
        <Select
          prefix={labels.limit}
          value={limit}
          style={{ width: 150 }}
          onChange={value => setLimit(typeof value === "number" ? value : 50)}
          optionList={[20, 50, 100, 200].map(value => ({ value, label: String(value) }))}
        />
      </Space>
      <SqlCodeEditor value={sql} onChange={setSql} height={compact ? 128 : 260} />
      {result ? (
        <Space vertical align="start" style={{ width: "100%" }}>
          <Space>
            <Text type="tertiary">{labels.affectedRows}: {result.affectedRows ?? "-"}</Text>
            <Text type="tertiary">{labels.elapsedMs}: {result.elapsedMs ?? "-"}ms</Text>
          </Space>
          <Table rowKey="__rowKey" size="small" pagination={false} columns={columns} dataSource={result.rows.map((row, index) => ({ ...row, __rowKey: String(index) }))} />
        </Space>
      ) : null}
    </Space>
  );
}

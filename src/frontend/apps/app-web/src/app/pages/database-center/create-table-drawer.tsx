import { useMemo, useState } from "react";
import { Button, Input, Select, SideSheet, Space, Switch, Tabs, Toast, Typography } from "@douyinfe/semi-ui";
import { IconDelete, IconPlus, IconSave, IconTickCircle } from "@douyinfe/semi-icons";
import {
  createTableSql,
  createTableVisual,
  previewCreateTableDdl,
  type TableColumnDesignDto
} from "../../../services/api-database-structure";
import { SqlCodeEditor } from "../database-structure/sql-code-editor";
import type { DatabaseCenterLabels } from "./database-center-labels";
import { formatSql } from "./sql-format-utils";

const { Text } = Typography;

interface CreateTableDrawerProps {
  labels: DatabaseCenterLabels;
  visible: boolean;
  sourceId: string;
  schemaName: string;
  onClose: () => void;
  onCreated: () => Promise<void> | void;
}

const typeOptions = ["INTEGER", "TEXT", "REAL", "NUMERIC", "BLOB", "DATETIME"].map(value => ({ value, label: value }));

function defaultColumns(): TableColumnDesignDto[] {
  return [
    { id: "id", name: "id", dataType: "INTEGER", nullable: false, primaryKey: true, autoIncrement: true },
    { id: "name", name: "name", dataType: "TEXT", length: 100, nullable: false, primaryKey: false, autoIncrement: false }
  ];
}

export function CreateTableDrawer({ labels, visible, sourceId, schemaName, onClose, onCreated }: CreateTableDrawerProps) {
  const [activeKey, setActiveKey] = useState("visual");
  const [tableName, setTableName] = useState("");
  const [comment, setComment] = useState("");
  const [columns, setColumns] = useState<TableColumnDesignDto[]>(defaultColumns);
  const [sql, setSql] = useState("CREATE TABLE e2e_order_record (\n  id INTEGER PRIMARY KEY AUTOINCREMENT,\n  user_id INTEGER NOT NULL,\n  order_no TEXT NOT NULL,\n  amount NUMERIC,\n  created_at DATETIME DEFAULT CURRENT_TIMESTAMP\n);");
  const [previewSql, setPreviewSql] = useState("");
  const [loading, setLoading] = useState(false);

  const validVisual = useMemo(() => tableName.trim() && columns.some(column => column.name.trim()), [columns, tableName]);

  return (
    <SideSheet visible={visible} onCancel={onClose} title={labels.createTableVisual} width={980}>
      <Tabs activeKey={activeKey} onChange={key => setActiveKey(String(key))}>
        <Tabs.TabPane tab={labels.createTableVisual} itemKey="visual">
          <Space vertical align="start" style={{ width: "100%" }}>
            <Space style={{ width: "100%" }}>
              <Input placeholder={labels.tableName} value={tableName} onChange={setTableName} style={{ width: 260 }} />
              <Input placeholder={labels.comment} value={comment} onChange={setComment} style={{ width: 320 }} />
              <Button icon={<IconPlus />} onClick={addColumn}>{labels.addColumn}</Button>
            </Space>
            <Space vertical align="start" style={{ width: "100%" }}>
              {columns.map((column, index) => (
                <div key={column.id || `${column.name}-${index}`} className="database-center-designer-row">
                  <Input
                    placeholder={labels.columnName}
                    value={column.name}
                    onChange={value => updateColumn(index, { name: value })}
                  />
                  <Select
                    value={column.dataType}
                    optionList={typeOptions}
                    onChange={value => updateColumn(index, { dataType: String(value) })}
                  />
                  <Input
                    placeholder={labels.defaultValue}
                    value={column.defaultValue ?? ""}
                    onChange={value => updateColumn(index, { defaultValue: value })}
                  />
                  <Space>
                    <Text>{labels.primaryKey}</Text>
                    <Switch checked={column.primaryKey} onChange={checked => updateColumn(index, { primaryKey: checked, nullable: checked ? false : column.nullable })} />
                  </Space>
                  <Space>
                    <Text>{labels.autoIncrement}</Text>
                    <Switch checked={column.autoIncrement} onChange={checked => updateColumn(index, { autoIncrement: checked })} />
                  </Space>
                  <Space>
                    <Text>{labels.nullable}</Text>
                    <Switch disabled={column.primaryKey} checked={column.nullable} onChange={checked => updateColumn(index, { nullable: checked })} />
                  </Space>
                  <Button icon={<IconDelete />} type="danger" disabled={columns.length <= 1} onClick={() => removeColumn(index)} />
                </div>
              ))}
            </Space>
            <Space>
              <Button icon={<IconTickCircle />} loading={loading} disabled={!validVisual} onClick={() => void previewVisual()}>{labels.previewSql}</Button>
              <Button icon={<IconSave />} theme="solid" loading={loading} disabled={!validVisual} onClick={() => void createVisual()}>{labels.create}</Button>
            </Space>
            <SqlCodeEditor value={previewSql} readOnly height={220} />
          </Space>
        </Tabs.TabPane>
        <Tabs.TabPane tab={labels.createTableSql} itemKey="sql">
          <Space vertical align="start" style={{ width: "100%" }}>
            <Space>
              <Button onClick={() => setSql(formatSql(sql))}>{labels.format}</Button>
              <Button icon={<IconSave />} theme="solid" loading={loading} disabled={!sql.trim()} onClick={() => void createBySql()}>{labels.execute}</Button>
            </Space>
            <SqlCodeEditor value={sql} onChange={setSql} height={420} />
          </Space>
        </Tabs.TabPane>
      </Tabs>
    </SideSheet>
  );

  function addColumn() {
    setColumns(current => [
      ...current,
      { id: `col-${Date.now()}-${current.length}`, name: "", dataType: "TEXT", nullable: true, primaryKey: false, autoIncrement: false }
    ]);
  }

  function updateColumn(index: number, patch: Partial<TableColumnDesignDto>) {
    setColumns(current => current.map((column, currentIndex) => currentIndex === index ? { ...column, ...patch } : column));
  }

  function removeColumn(index: number) {
    setColumns(current => current.filter((_column, currentIndex) => currentIndex !== index));
  }

  function visualPayload() {
    return {
      schema: schemaName,
      tableName: tableName.trim(),
      comment: comment.trim() || undefined,
      columns: columns
        .filter(column => column.name.trim())
        .map(column => ({ ...column, name: column.name.trim(), defaultValue: column.defaultValue?.trim() || undefined }))
    };
  }

  async function previewVisual() {
    setLoading(true);
    try {
      const result = await previewCreateTableDdl(sourceId, visualPayload());
      setPreviewSql(result.ddl);
      Toast.success(labels.previewSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }

  async function createVisual() {
    setLoading(true);
    try {
      await createTableVisual(sourceId, visualPayload());
      Toast.success(labels.createSuccess);
      await onCreated();
      onClose();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }

  async function createBySql() {
    setLoading(true);
    try {
      await createTableSql(sourceId, { schema: schemaName, sql });
      Toast.success(labels.createSuccess);
      await onCreated();
      onClose();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }
}

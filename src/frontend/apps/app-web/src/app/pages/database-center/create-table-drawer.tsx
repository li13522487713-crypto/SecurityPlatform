import { useMemo, useState, type DragEvent } from "react";
import { Button, Input, Select, SideSheet, Space, Switch, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
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
const auditFieldNames = new Set(["created_at", "created_by", "updated_at", "updated_by"]);
const fieldTemplates: TableColumnDesignDto[] = [
  { id: "tpl-id", name: "id", dataType: "INTEGER", nullable: false, primaryKey: true, autoIncrement: true },
  { id: "tpl-name", name: "name", dataType: "TEXT", length: 100, nullable: false, primaryKey: false, autoIncrement: false }
];

function defaultColumns(): TableColumnDesignDto[] {
  return [
    { id: "id", name: "id", dataType: "INTEGER", nullable: false, primaryKey: true, autoIncrement: true },
    { id: "name", name: "name", dataType: "TEXT", length: 100, nullable: false, primaryKey: false, autoIncrement: false },
    { id: "audit-created-at", name: "created_at", dataType: "DATETIME", nullable: true, primaryKey: false, autoIncrement: false, defaultValue: "CURRENT_TIMESTAMP" },
    { id: "audit-created-by", name: "created_by", dataType: "DATETIME", nullable: true, primaryKey: false, autoIncrement: false },
    { id: "audit-updated-at", name: "updated_at", dataType: "DATETIME", nullable: true, primaryKey: false, autoIncrement: false },
    { id: "audit-updated-by", name: "updated_by", dataType: "DATETIME", nullable: true, primaryKey: false, autoIncrement: false }
  ];
}

function isAuditColumn(column?: TableColumnDesignDto): boolean {
  return auditFieldNames.has(column?.name.trim().toLowerCase() ?? "");
}

function insertBeforeAuditFields(columns: TableColumnDesignDto[], column: TableColumnDesignDto): TableColumnDesignDto[] {
  const auditIndex = columns.findIndex(isAuditColumn);
  if (auditIndex < 0) {
    return [...columns, column];
  }

  return [...columns.slice(0, auditIndex), column, ...columns.slice(auditIndex)];
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
    <SideSheet visible={visible} onCancel={onClose} title={labels.createTableVisual} width="min(980px, calc(100vw - 32px))">
      <Tabs activeKey={activeKey} onChange={key => setActiveKey(String(key))}>
        <Tabs.TabPane tab={labels.createTableVisual} itemKey="visual">
          <Space vertical align="start" style={{ width: "100%" }}>
            <Space className="database-center-drawer-form-row" style={{ width: "100%" }}>
              <Input placeholder={labels.tableName} value={tableName} onChange={setTableName} style={{ flex: "1 1 220px", minWidth: 0 }} />
              <Input placeholder={labels.comment} value={comment} onChange={setComment} style={{ flex: "1 1 280px", minWidth: 0 }} />
              <Button icon={<IconPlus />} onClick={addColumn}>{labels.addColumn}</Button>
            </Space>
            <Space vertical align="start" style={{ width: "100%" }}>
              <div className="database-center-template-strip">
                <Text strong>{labels.fieldTemplates}</Text>
                <Text type="tertiary" size="small">{labels.dragTemplateHint}</Text>
                <Space wrap>
                  {fieldTemplates.map(template => (
                    <span
                      key={template.id}
                      draggable
                      onDragStart={(event: DragEvent<HTMLSpanElement>) => {
                        event.dataTransfer.setData("application/x-field-template", JSON.stringify(template));
                      }}
                    >
                      <Tag>{template.name} / {template.dataType}</Tag>
                    </span>
                  ))}
                  {typeOptions.map(option => (
                    <span
                      key={option.value}
                      draggable
                      onDragStart={(event: DragEvent<HTMLSpanElement>) => event.dataTransfer.setData("application/x-data-type", option.value)}
                    >
                      <Tag color="blue">{option.value}</Tag>
                    </span>
                  ))}
                </Space>
              </div>
              <div
                className="database-center-designer-dropzone"
                onDragOver={event => event.preventDefault()}
                onDrop={dropTemplate}
              >
              {columns.map((column, index) => (
                <div key={column.id || `${column.name}-${index}`} className="database-center-designer-row">
                  <Input
                    placeholder={labels.columnName}
                    value={column.name}
                    disabled={isAuditColumn(column)}
                    onChange={value => updateColumn(index, { name: value })}
                  />
                  <div onDragOver={event => event.preventDefault()} onDrop={event => dropDataType(event, index)}>
                    <Select
                      value={column.dataType}
                      optionList={typeOptions}
                      disabled={isAuditColumn(column)}
                      onChange={value => updateColumn(index, { dataType: String(value) })}
                    />
                  </div>
                  <Input
                    placeholder={labels.defaultValue}
                    value={column.defaultValue ?? ""}
                    disabled={isAuditColumn(column)}
                    onChange={value => updateColumn(index, { defaultValue: value })}
                  />
                  <Space>
                    <Text>{labels.primaryKey}</Text>
                    <Switch disabled={isAuditColumn(column)} checked={column.primaryKey} onChange={checked => updateColumn(index, { primaryKey: checked, nullable: checked ? false : column.nullable })} />
                  </Space>
                  <Space>
                    <Text>{labels.autoIncrement}</Text>
                    <Switch disabled={isAuditColumn(column)} checked={column.autoIncrement} onChange={checked => updateColumn(index, { autoIncrement: checked })} />
                  </Space>
                  <Space>
                    <Text>{labels.nullable}</Text>
                    <Switch disabled={column.primaryKey || isAuditColumn(column)} checked={column.nullable} onChange={checked => updateColumn(index, { nullable: checked })} />
                  </Space>
                  <Space>
                    <Button size="small" disabled={index === 0 || isAuditColumn(column) || isAuditColumn(columns[index - 1])} onClick={() => moveColumn(index, -1)}>{labels.moveUp}</Button>
                    <Button size="small" disabled={index === columns.length - 1 || isAuditColumn(column) || isAuditColumn(columns[index + 1])} onClick={() => moveColumn(index, 1)}>{labels.moveDown}</Button>
                    <Button icon={<IconDelete />} type="danger" disabled={columns.length <= 1 || isAuditColumn(column)} onClick={() => removeColumn(index)} />
                  </Space>
                </div>
              ))}
              </div>
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
    setColumns(current => insertBeforeAuditFields(current, { id: `col-${Date.now()}-${current.length}`, name: "", dataType: "TEXT", nullable: true, primaryKey: false, autoIncrement: false }));
  }

  function updateColumn(index: number, patch: Partial<TableColumnDesignDto>) {
    setColumns(current => current.map((column, currentIndex) => currentIndex === index && !isAuditColumn(column) ? { ...column, ...patch } : column));
  }

  function removeColumn(index: number) {
    setColumns(current => current.filter((column, currentIndex) => currentIndex !== index || isAuditColumn(column)));
  }

  function moveColumn(index: number, offset: -1 | 1) {
    setColumns(current => {
      const next = [...current];
      const target = index + offset;
      if (target < 0 || target >= next.length) return current;
      if (isAuditColumn(next[index]) || isAuditColumn(next[target])) return current;
      const [item] = next.splice(index, 1);
      next.splice(target, 0, item);
      return next;
    });
  }

  function dropTemplate(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    const raw = event.dataTransfer.getData("application/x-field-template");
    if (!raw) return;
    try {
      const template = JSON.parse(raw) as TableColumnDesignDto;
      setColumns(current => insertBeforeAuditFields(
        current,
        {
          ...template,
          id: `tpl-${Date.now()}-${current.length}`,
          name: current.some(item => item.name === template.name) ? `${template.name}_${current.length + 1}` : template.name
        }
      ));
    } catch {
      Toast.error(labels.loadFailed);
    }
  }

  function dropDataType(event: DragEvent<HTMLElement>, index: number) {
    event.preventDefault();
    const type = event.dataTransfer.getData("application/x-data-type");
    if (type && !isAuditColumn(columns[index])) {
      updateColumn(index, { dataType: type });
    }
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

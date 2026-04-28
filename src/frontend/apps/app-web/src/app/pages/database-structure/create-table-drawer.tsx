import { useEffect, useMemo, useState } from "react";
import { Button, Checkbox, Input, Select, SideSheet, Space, Table, Tabs, Toast } from "@douyinfe/semi-ui";
import { IconArrowDown, IconArrowUp, IconPlus } from "@douyinfe/semi-icons";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useAppI18n } from "../../i18n";
import {
  createTableSql,
  createTableVisual,
  previewCreateTableDdl,
  type TableColumnDesignDto
} from "../../../services/api-database-structure";
import { getTypeOptions } from "./data-type-options";
import { SqlCodeEditor } from "./sql-code-editor";

interface CreateTableDrawerProps {
  visible: boolean;
  databaseId: string;
  driverCode?: string;
  onClose: () => void;
  onCreated: () => Promise<void>;
}

function newColumnId(): string {
  return `col_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
}

function createDefaultColumns(): TableColumnDesignDto[] {
  return [
    { id: newColumnId(), name: "id", dataType: "INTEGER", nullable: false, primaryKey: true, autoIncrement: true },
    { id: newColumnId(), name: "name", dataType: "TEXT", nullable: true, primaryKey: false, autoIncrement: false },
    { id: newColumnId(), name: "created_at", dataType: "TEXT", nullable: true, primaryKey: false, autoIncrement: false }
  ];
}

function sqlTemplate(driverCode?: string): string {
  if (driverCode === "MySql") {
    return "CREATE TABLE demo_table (\n  id BIGINT PRIMARY KEY AUTO_INCREMENT,\n  name VARCHAR(128) NOT NULL,\n  created_at DATETIME NULL\n) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
  }

  if (driverCode === "PostgreSQL") {
    return "CREATE TABLE demo_table (\n  id BIGSERIAL PRIMARY KEY,\n  name VARCHAR(128) NOT NULL,\n  created_at TIMESTAMP NULL\n);";
  }

  return "CREATE TABLE demo_table (\n  id INTEGER PRIMARY KEY AUTOINCREMENT,\n  name TEXT NOT NULL,\n  created_at TEXT NULL\n);";
}

export function CreateTableDrawer({ visible, databaseId, driverCode, onClose, onCreated }: CreateTableDrawerProps) {
  const { t } = useAppI18n();
  const [mode, setMode] = useState("visual");
  const [tableName, setTableName] = useState("");
  const [comment, setComment] = useState("");
  const [schema, setSchema] = useState("");
  const [sql, setSql] = useState(sqlTemplate(driverCode));
  const [columns, setColumns] = useState<TableColumnDesignDto[]>(() => createDefaultColumns());
  const [ddl, setDdl] = useState("");
  const [busy, setBusy] = useState(false);
  const typeOptions = useMemo(() => getTypeOptions(driverCode), [driverCode]);

  useEffect(() => {
    if (!visible) {
      setMode("visual");
      setTableName("");
      setComment("");
      setSchema("");
      setColumns(createDefaultColumns());
      setDdl("");
      setSql(sqlTemplate(driverCode));
    }
  }, [driverCode, visible]);

  const visualRequest = {
    tableName,
    comment,
    schema: schema || undefined,
    columns,
    options: { schema: schema || undefined }
  };

  async function preview() {
    try {
      setBusy(true);
      const result = mode === "visual"
        ? await previewCreateTableDdl(databaseId, visualRequest)
        : { ddl: sql };
      setDdl(result.ddl);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("databaseStructureLoadFailed"));
    } finally {
      setBusy(false);
    }
  }

  async function create() {
    if (mode === "visual" && !validateVisual()) {
      return;
    }

    try {
      setBusy(true);
      if (mode === "visual") {
        await createTableVisual(databaseId, visualRequest);
      } else {
        await createTableSql(databaseId, { sql });
      }
      Toast.success(t("databaseStructureCreateSuccess"));
      onClose();
      await onCreated();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("databaseStructureLoadFailed"));
    } finally {
      setBusy(false);
    }
  }

  const tableColumns: ColumnProps<TableColumnDesignDto>[] = [
    {
      title: "",
      width: 84,
      render: (_: unknown, row: TableColumnDesignDto, index?: number) => (
        <Space spacing={0}>
          <Button icon={<IconArrowUp />} disabled={!index} theme="borderless" onClick={() => moveColumn(index ?? 0, -1)} />
          <Button icon={<IconArrowDown />} disabled={index == null || index >= columns.length - 1} theme="borderless" onClick={() => moveColumn(index ?? 0, 1)} />
        </Space>
      )
    },
    { title: t("databaseStructureFieldName"), render: (_: unknown, row, index?: number) => <Input value={row.name} onChange={value => updateColumn(index ?? 0, { name: value })} /> },
    {
      title: t("databaseStructureFieldType"),
      width: 160,
      render: (_: unknown, row, index?: number) => (
        <Select value={row.dataType} style={{ width: 140 }} onChange={value => updateColumn(index ?? 0, { dataType: String(value) })}>
          {typeOptions.map(type => <Select.Option key={type} value={type}>{type}</Select.Option>)}
        </Select>
      )
    },
    { title: t("databaseStructureNullable"), width: 80, render: (_: unknown, row, index?: number) => <Checkbox checked={row.nullable} onChange={event => updateColumn(index ?? 0, { nullable: Boolean(event.target.checked) })} /> },
    { title: t("databaseStructurePrimaryKey"), width: 80, render: (_: unknown, row, index?: number) => <Checkbox checked={row.primaryKey} onChange={event => setSinglePrimaryKey(index ?? 0, Boolean(event.target.checked))} /> },
    { title: t("databaseStructureAutoIncrement"), width: 80, render: (_: unknown, row, index?: number) => <Checkbox checked={row.autoIncrement} disabled={!row.primaryKey} onChange={event => updateColumn(index ?? 0, { autoIncrement: Boolean(event.target.checked) })} /> },
    { title: t("databaseStructureDefaultValue"), render: (_: unknown, row, index?: number) => <Input value={row.defaultValue} onChange={value => updateColumn(index ?? 0, { defaultValue: value })} /> },
    { title: t("databaseStructureComment"), render: (_: unknown, row, index?: number) => <Input value={row.comment} onChange={value => updateColumn(index ?? 0, { comment: value })} /> },
    {
      title: t("databaseStructureColumnActions"),
      width: 90,
      render: (_: unknown, row) => (
        <Button
          type="danger"
          theme="borderless"
          disabled={columns.length <= 1}
          onClick={() => setColumns(items => items.length <= 1 ? items : items.filter(item => item.id !== row.id))}
        >
          {t("databaseStructureRemove")}
        </Button>
      )
    }
  ];

  return (
    <SideSheet visible={visible} onCancel={onClose} title={t("databaseStructureNewTable")} width="min(1040px, calc(100vw - 32px))">
      <Tabs activeKey={mode} onChange={setMode}>
        <Tabs.TabPane tab={t("databaseStructureVisualDesign")} itemKey="visual" />
        <Tabs.TabPane tab={t("databaseStructureSqlCreate")} itemKey="sql" />
      </Tabs>
      {mode === "visual" ? (
        <Space vertical align="start" style={{ width: "100%" }}>
          <Input placeholder={t("databaseStructureTableName")} value={tableName} onChange={setTableName} />
          <Input placeholder="schema" value={schema} onChange={setSchema} />
          <Input placeholder={t("databaseStructureTableComment")} value={comment} onChange={setComment} />
          <div className="database-center-table-scroll">
            <Table rowKey="id" pagination={false} dataSource={columns} columns={tableColumns} size="small" scroll={{ x: 1120 }} />
          </div>
          <Space>
            <Button icon={<IconPlus />} onClick={() => setColumns(items => [...items, { id: newColumnId(), name: "", dataType: typeOptions[0] ?? "TEXT", nullable: true, primaryKey: false, autoIncrement: false }])}>{t("databaseStructureAddField")}</Button>
            <Button onClick={addAuditFields}>{t("databaseStructureAddAuditFields")}</Button>
          </Space>
        </Space>
      ) : (
        <SqlCodeEditor value={sql} onChange={setSql} height={340} />
      )}
      <Space style={{ marginTop: 16 }}>
        <Button onClick={onClose}>{t("databaseStructureCancel")}</Button>
        <Button loading={busy} onClick={() => void preview()}>{t("databaseStructurePreviewSql")}</Button>
        <Button loading={busy} theme="solid" onClick={() => void create()}>{t("databaseStructureCreate")}</Button>
      </Space>
      {ddl ? <div style={{ marginTop: 16 }}><SqlCodeEditor value={ddl} readOnly height={220} /></div> : null}
    </SideSheet>
  );

  function updateColumn(index: number, patch: Partial<TableColumnDesignDto>) {
    setColumns(items => items.map((item, i) => (i === index ? { ...item, ...patch } : item)));
  }

  function setSinglePrimaryKey(index: number, checked: boolean) {
    setColumns(items => items.map((item, i) => ({
      ...item,
      primaryKey: i === index ? checked : false,
      autoIncrement: i === index ? item.autoIncrement && checked : false
    })));
  }

  function moveColumn(index: number, direction: -1 | 1) {
    setColumns(items => {
      const nextIndex = index + direction;
      if (nextIndex < 0 || nextIndex >= items.length) return items;
      const copy = [...items];
      const [item] = copy.splice(index, 1);
      copy.splice(nextIndex, 0, item);
      return copy;
    });
  }

  function addAuditFields() {
    const additions: TableColumnDesignDto[] = [
      { id: newColumnId(), name: "created_at", dataType: typeOptions.includes("TIMESTAMP") ? "TIMESTAMP" : "TEXT", nullable: true, primaryKey: false, autoIncrement: false },
      { id: newColumnId(), name: "updated_at", dataType: typeOptions.includes("TIMESTAMP") ? "TIMESTAMP" : "TEXT", nullable: true, primaryKey: false, autoIncrement: false },
      { id: newColumnId(), name: "created_by", dataType: typeOptions.includes("VARCHAR") ? "VARCHAR" : "TEXT", length: 64, nullable: true, primaryKey: false, autoIncrement: false },
      { id: newColumnId(), name: "updated_by", dataType: typeOptions.includes("VARCHAR") ? "VARCHAR" : "TEXT", length: 64, nullable: true, primaryKey: false, autoIncrement: false },
      { id: newColumnId(), name: "is_deleted", dataType: typeOptions.includes("BOOLEAN") ? "BOOLEAN" : "INTEGER", nullable: false, primaryKey: false, autoIncrement: false, defaultValue: "0" }
    ];
    setColumns(items => {
      const names = new Set(items.map(item => item.name));
      return [...items, ...additions.filter(item => !names.has(item.name))];
    });
  }

  function validateVisual(): boolean {
    if (!tableName.trim()) {
      Toast.warning(t("databaseStructureTableName"));
      return false;
    }
    const names = columns.map(column => column.name.trim()).filter(Boolean);
    if (names.length !== columns.length) {
      Toast.warning(t("databaseStructureFieldName"));
      return false;
    }
    if (new Set(names).size !== names.length) {
      Toast.warning(t("databaseStructureFieldName"));
      return false;
    }
    return true;
  }
}

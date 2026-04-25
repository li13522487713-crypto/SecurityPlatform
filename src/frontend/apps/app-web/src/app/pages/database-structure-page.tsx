import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Button, Checkbox, Dropdown, Input, Modal, Select, SideSheet, Space, Spin, Table, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconArrowLeft, IconChevronDown, IconPlus, IconRefresh } from "@douyinfe/semi-icons";
import { useAppI18n } from "../i18n";
import { getAiDatabaseById, type AiDatabaseDetail } from "../../services/api-ai-database";
import {
  createTableSql,
  createTableVisual,
  createView,
  dropTable,
  dropView,
  getTableColumns,
  getTableDdl,
  getViewColumns,
  getViewDdl,
  listDatabaseObjects,
  previewCreateTableDdl,
  previewTableData,
  previewViewData,
  previewViewSql,
  type DatabaseColumnDto,
  type DatabaseObjectDto,
  type DatabaseObjectType,
  type PreviewDataResponse,
  type TableColumnDesignDto
} from "../../services/api-database-structure";
import { getTypeOptions } from "./database-structure/data-type-options";

const { Text, Title } = Typography;

const DEFAULT_COLUMN: TableColumnDesignDto = {
  name: "id",
  dataType: "INTEGER",
  nullable: false,
  primaryKey: true,
  autoIncrement: true
};

export function DatabaseStructurePage() {
  const { databaseId = "" } = useParams();
  const navigate = useNavigate();
  const { t } = useAppI18n();
  const [database, setDatabase] = useState<AiDatabaseDetail | null>(null);
  const [activeType, setActiveType] = useState<DatabaseObjectType>("table");
  const [objects, setObjects] = useState<DatabaseObjectDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [createTableVisible, setCreateTableVisible] = useState(false);
  const [createViewVisible, setCreateViewVisible] = useState(false);
  const [detailTarget, setDetailTarget] = useState<DatabaseObjectDto | null>(null);

  const load = useCallback(async () => {
    if (!databaseId) return;
    setLoading(true);
    try {
      const [detail, items] = await Promise.all([
        getAiDatabaseById(Number(databaseId)),
        listDatabaseObjects(databaseId, activeType)
      ]);
      setDatabase(detail);
      setObjects(items);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("databaseStructureLoadFailed"));
    } finally {
      setLoading(false);
    }
  }, [activeType, databaseId, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns = useMemo<ColumnProps<DatabaseObjectDto>[]>(() => [
    { title: t("databaseStructureColumnName"), dataIndex: "name", render: (_: unknown, record) => <Button theme="borderless" onClick={() => setDetailTarget(record)}>{record.name}</Button> },
    { title: t("databaseStructureColumnType"), dataIndex: "objectType", width: 120 },
    { title: t("databaseStructureColumnEngine"), dataIndex: "engine", width: 140, render: (value: unknown) => value || "-" },
    { title: t("databaseStructureColumnRows"), dataIndex: "rowCount", width: 120, render: (value: unknown) => value ?? "-" },
    { title: t("databaseStructureColumnCreatedAt"), dataIndex: "createdAt", width: 180, render: (value: unknown) => value || "-" },
    { title: t("databaseStructureColumnUpdatedAt"), dataIndex: "updatedAt", width: 180, render: (value: unknown) => value || "-" },
    { title: t("databaseStructureColumnComment"), dataIndex: "comment", render: (value: unknown) => value || "-" },
    {
      title: t("databaseStructureColumnActions"),
      width: 150,
      render: (_: unknown, record) => (
        <Dropdown
          render={
            <Dropdown.Menu>
              <Dropdown.Item onClick={() => setDetailTarget(record)}>{t("databaseStructureActionStructure")}</Dropdown.Item>
              <Dropdown.Item onClick={() => setDetailTarget(record)}>{t("databaseStructureActionPreview")}</Dropdown.Item>
              <Dropdown.Item onClick={() => setDetailTarget(record)}>{t("databaseStructureActionDdl")}</Dropdown.Item>
              {record.objectType === "table" || record.objectType === "view" ? (
                <Dropdown.Item type="danger" onClick={() => handleDrop(record)}>{t("databaseStructureActionDelete")}</Dropdown.Item>
              ) : null}
            </Dropdown.Menu>
          }
        >
          <Button icon={<IconChevronDown />} theme="borderless">{t("databaseStructureMore")}</Button>
        </Dropdown>
      )
    }
  ], [t]);

  const handleDrop = useCallback((record: DatabaseObjectDto) => {
    let confirmName = "";
    Modal.confirm({
      title: t("databaseStructureDeleteTitle"),
      content: (
        <div>
          <Text>{t("databaseStructureDeleteContent")}</Text>
          <Input style={{ marginTop: 12 }} placeholder={record.name} onChange={value => { confirmName = value; }} />
        </div>
      ),
      okType: "danger",
      onOk: async () => {
        if (record.objectType === "table") {
          await dropTable(databaseId, record.name, { schema: record.schema, confirmName, confirmDanger: true });
        } else if (record.objectType === "view") {
          await dropView(databaseId, record.name, { schema: record.schema, confirmName, confirmDanger: true });
        }
        Toast.success(t("databaseStructureDeleteSuccess"));
        await load();
      }
    });
  }, [databaseId, load, t]);

  return (
    <div className="coze-page">
      <div className="coze-page__header">
        <Space vertical align="start" spacing={8}>
          <Button icon={<IconArrowLeft />} theme="borderless" onClick={() => navigate(-1)}>{t("databaseStructureBack")}</Button>
          <Space>
            <Title heading={3} style={{ margin: 0 }}>{database?.name ?? databaseId}</Title>
            <Tag color="blue">{database?.driverCode ?? "SQLite"}</Tag>
            <Tag color={database?.provisionState === "Ready" ? "green" : "orange"}>{database?.provisionState ?? "Pending"}</Tag>
          </Space>
          <Text type="tertiary">{t("databaseStructureBreadcrumb")}</Text>
        </Space>
        <Space>
          <Button icon={<IconRefresh />} onClick={() => void load()}>{t("databaseStructureRefresh")}</Button>
          <Button disabled>{t("databaseStructureImportDdl")}</Button>
          <Dropdown
            render={
              <Dropdown.Menu>
                <Dropdown.Item onClick={() => setCreateTableVisible(true)}>{t("databaseStructureNewTable")}</Dropdown.Item>
                <Dropdown.Item onClick={() => setCreateViewVisible(true)}>{t("databaseStructureNewView")}</Dropdown.Item>
              </Dropdown.Menu>
            }
          >
            <Button theme="solid" icon={<IconPlus />}>{t("databaseStructureNew")}</Button>
          </Dropdown>
        </Space>
      </div>

      <Tabs activeKey={activeType} onChange={key => setActiveType(key as DatabaseObjectType)}>
        <Tabs.TabPane tab={t("databaseStructureTabTables")} itemKey="table" />
        <Tabs.TabPane tab={t("databaseStructureTabViews")} itemKey="view" />
        <Tabs.TabPane tab={t("databaseStructureTabProcedures")} itemKey="procedure" />
        <Tabs.TabPane tab={t("databaseStructureTabTriggers")} itemKey="trigger" />
      </Tabs>
      <Spin spinning={loading}>
        <Table rowKey="name" columns={columns} dataSource={objects} pagination={false} />
      </Spin>

      <CreateTableDrawer
        visible={createTableVisible}
        databaseId={databaseId}
        driverCode={database?.driverCode}
        onClose={() => setCreateTableVisible(false)}
        onCreated={load}
      />
      <CreateViewDrawer
        visible={createViewVisible}
        databaseId={databaseId}
        onClose={() => setCreateViewVisible(false)}
        onCreated={load}
      />
      <ObjectDetailDrawer
        databaseId={databaseId}
        object={detailTarget}
        onClose={() => setDetailTarget(null)}
      />
    </div>
  );
}

function CreateTableDrawer(props: { visible: boolean; databaseId: string; driverCode?: string; onClose: () => void; onCreated: () => Promise<void> }) {
  const { t } = useAppI18n();
  const [mode, setMode] = useState("visual");
  const [tableName, setTableName] = useState("");
  const [comment, setComment] = useState("");
  const [sql, setSql] = useState("CREATE TABLE demo_table (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL);");
  const [columns, setColumns] = useState<TableColumnDesignDto[]>([DEFAULT_COLUMN]);
  const [ddl, setDdl] = useState("");
  const typeOptions = getTypeOptions(props.driverCode);

  const visualRequest = { tableName, comment, columns };

  async function preview() {
    const result = mode === "visual"
      ? await previewCreateTableDdl(props.databaseId, visualRequest)
      : { ddl: sql };
    setDdl(result.ddl);
  }

  async function create() {
    if (mode === "visual") {
      await createTableVisual(props.databaseId, visualRequest);
    } else {
      await createTableSql(props.databaseId, { sql });
    }
    Toast.success(t("databaseStructureCreateSuccess"));
    props.onClose();
    await props.onCreated();
  }

  return (
    <SideSheet visible={props.visible} onCancel={props.onClose} title={t("databaseStructureNewTable")} width={900}>
      <Tabs activeKey={mode} onChange={setMode}>
        <Tabs.TabPane tab={t("databaseStructureVisualDesign")} itemKey="visual" />
        <Tabs.TabPane tab={t("databaseStructureSqlCreate")} itemKey="sql" />
      </Tabs>
      {mode === "visual" ? (
        <Space vertical align="stretch" style={{ width: "100%" }}>
          <Input placeholder={t("databaseStructureTableName")} value={tableName} onChange={setTableName} />
          <Input placeholder={t("databaseStructureTableComment")} value={comment} onChange={setComment} />
          <Table
            rowKey={(_, index) => String(index)}
            pagination={false}
            dataSource={columns}
            columns={[
              { title: t("databaseStructureFieldName"), render: (_: unknown, row: TableColumnDesignDto, index?: number) => <Input value={row.name} onChange={value => updateColumn(index!, { name: value })} /> },
              { title: t("databaseStructureFieldType"), render: (_: unknown, row: TableColumnDesignDto, index?: number) => <Select value={row.dataType} style={{ width: 140 }} onChange={value => updateColumn(index!, { dataType: String(value) })}>{typeOptions.map(type => <Select.Option key={type} value={type}>{type}</Select.Option>)}</Select> },
              { title: t("databaseStructureNullable"), render: (_: unknown, row: TableColumnDesignDto, index?: number) => <Checkbox checked={row.nullable} onChange={event => updateColumn(index!, { nullable: Boolean(event.target.checked) })} /> },
              { title: t("databaseStructurePrimaryKey"), render: (_: unknown, row: TableColumnDesignDto, index?: number) => <Checkbox checked={row.primaryKey} onChange={event => updateColumn(index!, { primaryKey: Boolean(event.target.checked) })} /> },
              { title: t("databaseStructureAutoIncrement"), render: (_: unknown, row: TableColumnDesignDto, index?: number) => <Checkbox checked={row.autoIncrement} onChange={event => updateColumn(index!, { autoIncrement: Boolean(event.target.checked) })} /> },
              { title: t("databaseStructureDefaultValue"), render: (_: unknown, row: TableColumnDesignDto, index?: number) => <Input value={row.defaultValue} onChange={value => updateColumn(index!, { defaultValue: value })} /> },
              { title: t("databaseStructureColumnActions"), render: (_: unknown, __: TableColumnDesignDto, index?: number) => <Button type="danger" theme="borderless" onClick={() => setColumns(items => items.filter((_, i) => i !== index))}>{t("databaseStructureRemove")}</Button> }
            ]}
          />
          <Button onClick={() => setColumns(items => [...items, { name: "", dataType: typeOptions[0], nullable: true, primaryKey: false, autoIncrement: false }])}>{t("databaseStructureAddField")}</Button>
        </Space>
      ) : (
        <Input.TextArea rows={12} value={sql} onChange={setSql} />
      )}
      <Space style={{ marginTop: 16 }}>
        <Button onClick={props.onClose}>{t("databaseStructureCancel")}</Button>
        <Button onClick={() => void preview()}>{t("databaseStructurePreviewSql")}</Button>
        <Button theme="solid" onClick={() => void create()}>{t("databaseStructureCreate")}</Button>
      </Space>
      {ddl ? <Input.TextArea readonly rows={8} value={ddl} style={{ marginTop: 16 }} /> : null}
    </SideSheet>
  );

  function updateColumn(index: number, patch: Partial<TableColumnDesignDto>) {
    setColumns(items => items.map((item, i) => (i === index ? { ...item, ...patch } : item)));
  }
}

function CreateViewDrawer(props: { visible: boolean; databaseId: string; onClose: () => void; onCreated: () => Promise<void> }) {
  const { t } = useAppI18n();
  const [viewName, setViewName] = useState("");
  const [sql, setSql] = useState("SELECT 1 AS id");
  const [preview, setPreview] = useState<PreviewDataResponse | null>(null);

  async function handlePreview() {
    setPreview(await previewViewSql(props.databaseId, { sql, limit: 100 }));
  }

  async function handleCreate() {
    await createView(props.databaseId, { viewName, sql });
    Toast.success(t("databaseStructureCreateSuccess"));
    props.onClose();
    await props.onCreated();
  }

  return (
    <SideSheet visible={props.visible} onCancel={props.onClose} title={t("databaseStructureNewView")} width={820}>
      <Space vertical align="stretch" style={{ width: "100%" }}>
        <Input placeholder={t("databaseStructureViewName")} value={viewName} onChange={setViewName} />
        <Input.TextArea rows={12} value={sql} onChange={setSql} />
        <Space>
          <Button onClick={props.onClose}>{t("databaseStructureCancel")}</Button>
          <Button onClick={() => void handlePreview()}>{t("databaseStructurePreview")}</Button>
          <Button theme="solid" onClick={() => void handleCreate()}>{t("databaseStructureCreateView")}</Button>
        </Space>
        {preview ? <DataPreviewTable data={preview} /> : null}
      </Space>
    </SideSheet>
  );
}

function ObjectDetailDrawer(props: { databaseId: string; object: DatabaseObjectDto | null; onClose: () => void }) {
  const { t } = useAppI18n();
  const [columns, setColumns] = useState<DatabaseColumnDto[]>([]);
  const [ddl, setDdl] = useState("");
  const [preview, setPreview] = useState<PreviewDataResponse | null>(null);

  useEffect(() => {
    if (!props.object) return;
    const load = async () => {
      const isView = props.object?.objectType === "view";
      const [cols, ddlResult, data] = await Promise.all([
        isView ? getViewColumns(props.databaseId, props.object.name, props.object.schema) : getTableColumns(props.databaseId, props.object.name, props.object.schema),
        isView ? getViewDdl(props.databaseId, props.object.name, props.object.schema) : getTableDdl(props.databaseId, props.object.name, props.object.schema),
        isView
          ? previewViewData(props.databaseId, props.object.name, { pageIndex: 1, pageSize: 10, environment: "Draft" })
          : previewTableData(props.databaseId, props.object.name, { pageIndex: 1, pageSize: 10, environment: "Draft" })
      ]);
      setColumns(cols);
      setDdl(ddlResult.ddl);
      setPreview(data);
    };
    void load().catch(error => Toast.error(error instanceof Error ? error.message : t("databaseStructureLoadFailed")));
  }, [props.databaseId, props.object, t]);

  return (
    <SideSheet visible={Boolean(props.object)} onCancel={props.onClose} title={props.object?.name} width={920}>
      <Tabs>
        <Tabs.TabPane tab={t("databaseStructureTabStructure")} itemKey="structure">
          <Table rowKey="name" pagination={false} dataSource={columns} columns={[
            { title: t("databaseStructureFieldName"), dataIndex: "name" },
            { title: t("databaseStructureFieldType"), dataIndex: "dataType" },
            { title: t("databaseStructureNullable"), dataIndex: "nullable", render: (value: unknown) => String(value) },
            { title: t("databaseStructurePrimaryKey"), dataIndex: "primaryKey", render: (value: unknown) => String(value) },
            { title: t("databaseStructureDefaultValue"), dataIndex: "defaultValue" },
            { title: t("databaseStructureComment"), dataIndex: "comment" }
          ]} />
        </Tabs.TabPane>
        <Tabs.TabPane tab={t("databaseStructureTabPreview")} itemKey="preview">
          {preview ? <DataPreviewTable data={preview} /> : null}
        </Tabs.TabPane>
        <Tabs.TabPane tab={t("databaseStructureTabDdl")} itemKey="ddl">
          <Button onClick={() => navigator.clipboard?.writeText(ddl)}>{t("databaseStructureCopy")}</Button>
          <Input.TextArea rows={18} readonly value={ddl} style={{ marginTop: 12 }} />
        </Tabs.TabPane>
      </Tabs>
    </SideSheet>
  );
}

function DataPreviewTable(props: { data: PreviewDataResponse }) {
  const columns = props.data.columns.map(column => ({
    title: column.name,
    dataIndex: column.name,
    render: (value: unknown) => {
      const text = value == null ? "" : String(value);
      return text.length > 120 ? `${text.slice(0, 120)}...` : text;
    }
  }));
  return <Table rowKey={(_, index) => String(index)} columns={columns} dataSource={props.data.rows} pagination={false} />;
}

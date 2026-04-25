import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Button, Dropdown, Space, Spin, Table, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconArrowLeft, IconChevronDown, IconPlus, IconRefresh } from "@douyinfe/semi-icons";
import { useAppI18n } from "../i18n";
import { getAiDatabaseById, type AiDatabaseDetail } from "../../services/api-ai-database";
import {
  dropTable,
  dropView,
  listDatabaseObjects,
  type DatabaseObjectDto,
  type DatabaseObjectType
} from "../../services/api-database-structure";
import { CreateTableDrawer } from "./database-structure/create-table-drawer";
import { CreateViewDrawer } from "./database-structure/create-view-drawer";
import { DangerDeleteModal } from "./database-structure/danger-delete-modal";
import { ObjectDetailDrawer } from "./database-structure/object-detail-drawer";

const { Text, Title } = Typography;

export function DatabaseStructurePage() {
  const { databaseId = "" } = useParams();
  const navigate = useNavigate();
  const { t } = useAppI18n();
  const [database, setDatabase] = useState<AiDatabaseDetail | null>(null);
  const [activeType, setActiveType] = useState<DatabaseObjectType>("table");
  const [objects, setObjects] = useState<DatabaseObjectDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [createTableVisible, setCreateTableVisible] = useState(false);
  const [createViewVisible, setCreateViewVisible] = useState(false);
  const [detailTarget, setDetailTarget] = useState<DatabaseObjectDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<DatabaseObjectDto | null>(null);

  const load = useCallback(async () => {
    if (!databaseId) return;
    setLoading(true);
    try {
      const [detail, items] = await Promise.all([
        getAiDatabaseById(databaseId),
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
    { title: "schema", dataIndex: "schema", width: 120, render: (value: unknown) => value || "-" },
    { title: t("databaseStructureColumnEngine"), dataIndex: "engine", width: 140, render: (_: unknown, record) => record.engine || record.algorithm || "-" },
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
              <Dropdown.Item disabled>{t("databaseStructureActionSqlQuery")}</Dropdown.Item>
              {record.objectType === "table" || record.objectType === "view" ? (
                <Dropdown.Item disabled={record.canDrop === false} type="danger" onClick={() => setDeleteTarget(record)}>{t("databaseStructureActionDelete")}</Dropdown.Item>
              ) : null}
            </Dropdown.Menu>
          }
        >
          <Button icon={<IconChevronDown />} theme="borderless">{t("databaseStructureMore")}</Button>
        </Dropdown>
      )
    }
  ], [t]);

  async function handleDelete(confirmName: string) {
    if (!deleteTarget) return;
    try {
      setDeleting(true);
      if (deleteTarget.objectType === "table") {
        await dropTable(databaseId, deleteTarget.name, { schema: deleteTarget.schema, confirmName, confirmDanger: true });
      } else if (deleteTarget.objectType === "view") {
        await dropView(databaseId, deleteTarget.name, { schema: deleteTarget.schema, confirmName, confirmDanger: true });
      }
      Toast.success(t("databaseStructureDeleteSuccess"));
      setDeleteTarget(null);
      setDetailTarget(current => current?.name === deleteTarget.name ? null : current);
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("databaseStructureLoadFailed"));
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div className="coze-page">
      <div className="coze-page__header">
        <Space vertical align="start" spacing={8}>
          <Button icon={<IconArrowLeft />} theme="borderless" onClick={() => navigate(-1)}>{t("databaseStructureBack")}</Button>
          <Space>
            <Title heading={3} style={{ margin: 0 }}>{database?.name ?? databaseId}</Title>
            <Tag color="blue">{database?.driverCode ?? "SQLite"}</Tag>
            <Tag color="green">draft</Tag>
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
        <Table rowKey={record => `${record.schema ?? ""}.${record.name}`} columns={columns} dataSource={objects} pagination={false} />
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
      <DangerDeleteModal
        visible={Boolean(deleteTarget)}
        object={deleteTarget}
        loading={deleting}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={handleDelete}
      />
    </div>
  );
}

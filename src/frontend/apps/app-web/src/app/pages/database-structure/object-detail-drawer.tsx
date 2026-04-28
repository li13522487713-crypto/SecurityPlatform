import { useEffect, useState } from "react";
import { Button, Select, SideSheet, Space, Spin, Table, Tabs, Toast } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useAppI18n } from "../../i18n";
import {
  getTableColumns,
  getTableDdl,
  getViewColumns,
  getViewDdl,
  previewTableData,
  previewViewData,
  type DatabaseColumnDto,
  type DatabaseObjectDto,
  type PreviewDataResponse
} from "../../../services/api-database-structure";
import { DataPreviewTable } from "./data-preview-table";
import { SqlCodeEditor } from "./sql-code-editor";

interface ObjectDetailDrawerProps {
  databaseId: string;
  object: DatabaseObjectDto | null;
  initialTab?: "structure" | "preview" | "ddl";
  onClose: () => void;
}

export function ObjectDetailDrawer({ databaseId, object, initialTab = "structure", onClose }: ObjectDetailDrawerProps) {
  const { t } = useAppI18n();
  const [activeTab, setActiveTab] = useState(initialTab);
  const [columns, setColumns] = useState<DatabaseColumnDto[]>([]);
  const [ddl, setDdl] = useState("");
  const [preview, setPreview] = useState<PreviewDataResponse | null>(null);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!object) return;
    setActiveTab(initialTab);
    void loadAll();
  }, [databaseId, initialTab, object?.name, object?.objectType, object?.schema, pageSize]);

  const structureColumns: ColumnProps<DatabaseColumnDto>[] = [
    { title: t("databaseStructureFieldName"), dataIndex: "name", width: 180 },
    { title: t("databaseStructureFieldType"), dataIndex: "dataType", width: 160 },
    { title: t("databaseStructureNullable"), dataIndex: "nullable", width: 110, render: (value: unknown) => String(value) },
    { title: t("databaseStructurePrimaryKey"), dataIndex: "primaryKey", width: 110, render: (value: unknown) => String(value) },
    { title: t("databaseStructureAutoIncrement"), dataIndex: "autoIncrement", width: 120, render: (value: unknown) => String(value) },
    { title: t("databaseStructureDefaultValue"), dataIndex: "defaultValue", width: 180 },
    { title: t("databaseStructureComment"), dataIndex: "comment", width: 220 },
    { title: "ordinal", dataIndex: "ordinal", width: 90 }
  ];

  return (
    <SideSheet visible={Boolean(object)} onCancel={onClose} title={object?.name} width="min(980px, calc(100vw - 32px))">
      <Spin spinning={loading}>
        <Tabs activeKey={activeTab} onChange={key => setActiveTab(key as "structure" | "preview" | "ddl")}>
          <Tabs.TabPane tab={t("databaseStructureTabStructure")} itemKey="structure">
            <div className="database-center-table-scroll">
              <Table rowKey="name" pagination={false} dataSource={columns} columns={structureColumns} size="small" scroll={{ x: 1170 }} />
            </div>
          </Tabs.TabPane>
          <Tabs.TabPane tab={t("databaseStructureTabPreview")} itemKey="preview">
            <Space vertical align="start" style={{ width: "100%" }}>
              <Space>
                <Select
                  value={pageSize}
                  style={{ width: 120 }}
                  onChange={value => setPageSize(typeof value === "number" ? value : 10)}
                  optionList={[10, 20, 50, 100].map(value => ({ value, label: String(value) }))}
                />
                <Button onClick={() => void loadPreview()}>{t("databaseStructureRefresh")}</Button>
              </Space>
              {preview ? <DataPreviewTable data={preview} /> : null}
            </Space>
          </Tabs.TabPane>
          <Tabs.TabPane tab={t("databaseStructureTabDdl")} itemKey="ddl">
            <Space vertical align="start" style={{ width: "100%" }}>
              <Space>
                <Button onClick={() => navigator.clipboard?.writeText(ddl)}>{t("databaseStructureCopy")}</Button>
                <Button onClick={() => void loadDdl()}>{t("databaseStructureRefresh")}</Button>
              </Space>
              <SqlCodeEditor value={ddl} readOnly height={420} />
            </Space>
          </Tabs.TabPane>
        </Tabs>
      </Spin>
    </SideSheet>
  );

  async function loadAll() {
    if (!object) return;
    try {
      setLoading(true);
      await Promise.all([loadColumns(), loadDdl(), loadPreview()]);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("databaseStructureLoadFailed"));
    } finally {
      setLoading(false);
    }
  }

  async function loadColumns() {
    if (!object) return;
    const isView = object.objectType === "view";
    setColumns(isView ? await getViewColumns(databaseId, object.name, object.schema) : await getTableColumns(databaseId, object.name, object.schema));
  }

  async function loadDdl() {
    if (!object) return;
    const isView = object.objectType === "view";
    const result = isView ? await getViewDdl(databaseId, object.name, object.schema) : await getTableDdl(databaseId, object.name, object.schema);
    setDdl(result.ddl);
  }

  async function loadPreview() {
    if (!object) return;
    const request = { schema: object.schema, pageIndex: 1, pageSize };
    setPreview(object.objectType === "view"
      ? await previewViewData(databaseId, object.name, request)
      : await previewTableData(databaseId, object.name, request));
  }
}

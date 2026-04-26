import { useEffect, useState } from "react";
import type React from "react";
import { Button, Empty, Space, Spin, Table, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import type {
  DatabaseCenterObjectSummary,
  DatabaseCenterSchemaStructure,
  DatabaseCenterSourceDetail,
  DatabaseCenterSourceSummary
} from "../../../services/api-database-center";
import { getTableDdl, getViewDdl, previewTableData, previewViewData, type PreviewDataResponse } from "../../../services/api-database-structure";
import { DataPreviewTable } from "../database-structure/data-preview-table";
import { SqlCodeEditor } from "../database-structure/sql-code-editor";
import { SqlEditorPanel } from "./sql-editor-panel";
import type { DatabaseCenterLabels } from "./database-center-labels";

const { Text } = Typography;

interface DatabaseCenterDockProps {
  labels: DatabaseCenterLabels;
  source: DatabaseCenterSourceSummary | DatabaseCenterSourceDetail | null;
  selectedObject: DatabaseCenterObjectSummary | null;
  structure: DatabaseCenterSchemaStructure | null;
  onSelectObject: (object: DatabaseCenterObjectSummary) => void;
}

export function DatabaseCenterDock({ labels, source, selectedObject, structure, onSelectObject }: DatabaseCenterDockProps) {
  const [ddl, setDdl] = useState("");
  const [preview, setPreview] = useState<PreviewDataResponse | null>(null);
  const [loadingDdl, setLoadingDdl] = useState(false);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [visibleCards, setVisibleCards] = useState<Record<string, boolean>>({
    tables: true,
    sql: true,
    preview: true,
    ddl: true
  });
  const tables = (structure?.objects ?? []).filter(item => item.objectType === "table");
  const tableColumns: ColumnProps<DatabaseCenterObjectSummary>[] = [
    {
      title: labels.name,
      dataIndex: "name",
      width: 180,
      render: (_value: unknown, record) => (
        <Button theme="borderless" size="small" onClick={() => onSelectObject(record)}>
          <Text ellipsis={{ showTooltip: true }} style={{ maxWidth: 140 }}>{record.name}</Text>
        </Button>
      )
    },
    { title: "Rows", dataIndex: "rowCount", width: 80, render: (value: unknown) => value == null ? "-" : String(value) },
    { title: labels.updatedAt, dataIndex: "updatedAt", width: 150, render: (value: unknown) => value ? String(value).slice(0, 19) : "-" }
  ];

  useEffect(() => {
    setDdl("");
    setPreview(null);
    if (source?.id && selectedObject && (selectedObject.objectType === "table" || selectedObject.objectType === "view")) {
      void loadDdl();
      void loadPreview();
    }
  }, [selectedObject?.id, selectedObject?.name, selectedObject?.objectType, selectedObject?.schema, source?.id]);

  return (
    <div className="database-center-dock">
      <DockCard title={`${labels.tableList} - ${structure?.schemaName ?? "-"}`} visible={visibleCards.tables} onClose={() => hide("tables")}>
        <div className="database-center-table-scroll">
          <Table
            rowKey={record => [record.schema, record.objectType, record.name, record.id].filter(Boolean).join(":")}
            size="small"
            pagination={false}
            columns={tableColumns}
            dataSource={tables}
            scroll={{ x: 410 }}
          />
        </div>
      </DockCard>
      <DockCard title={labels.sqlEditor} visible={visibleCards.sql} onClose={() => hide("sql")}>
        {source ? <SqlEditorPanel labels={labels} sourceId={source.id} schema={structure?.schemaName} environment={source.environment ?? "Draft"} compact /> : <Empty description={labels.noSource} />}
      </DockCard>
      <DockCard title={`${labels.dataPreview} - ${selectedObject?.name ?? "-"}`} visible={visibleCards.preview} onClose={() => hide("preview")}>
        <Spin spinning={loadingPreview}>
          <Space vertical align="stretch" style={{ width: "100%" }}>
            <Button size="small" disabled={!selectedObject} onClick={() => void loadPreview()}>{labels.refresh}</Button>
            {preview ? <DataPreviewTable data={preview} /> : <Empty description={labels.noObjectSelected} />}
          </Space>
        </Spin>
      </DockCard>
      <DockCard title={`${labels.ddl} - ${selectedObject?.name ?? "-"}`} visible={visibleCards.ddl} onClose={() => hide("ddl")}>
        <Spin spinning={loadingDdl}>
          <Space vertical align="stretch" style={{ width: "100%" }}>
            <Space>
              <Button size="small" disabled={!selectedObject} onClick={() => void loadDdl()}>{labels.refresh}</Button>
              <Button size="small" disabled={!ddl} onClick={() => navigator.clipboard?.writeText(ddl)}>{labels.copy}</Button>
              <Button size="small" disabled={!ddl} onClick={() => downloadText(ddl, `${selectedObject?.name ?? "ddl"}.sql`)}>{labels.download}</Button>
            </Space>
            {ddl ? <SqlCodeEditor value={ddl} readOnly height={150} /> : <Empty description={labels.noObjectSelected} />}
          </Space>
        </Spin>
      </DockCard>
    </div>
  );

  function hide(key: string) {
    setVisibleCards(current => ({ ...current, [key]: false }));
  }

  async function loadDdl() {
    if (!source?.id || !selectedObject) return;
    if (selectedObject.objectType !== "table" && selectedObject.objectType !== "view") {
      setDdl("");
      return;
    }

    setLoadingDdl(true);
    try {
      const result = selectedObject.objectType === "table"
        ? await getTableDdl(source.id, selectedObject.name, selectedObject.schema ?? undefined)
        : await getViewDdl(source.id, selectedObject.name, selectedObject.schema ?? undefined);
      setDdl(result.ddl);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoadingDdl(false);
    }
  }

  async function loadPreview() {
    if (!source?.id || !selectedObject) return;
    if (selectedObject.objectType !== "table" && selectedObject.objectType !== "view") {
      setPreview(null);
      return;
    }

    setLoadingPreview(true);
    try {
      const request = { schema: selectedObject.schema ?? structure?.schemaName, pageIndex: 1, pageSize: 10 };
      setPreview(selectedObject.objectType === "table"
        ? await previewTableData(source.id, selectedObject.name, request)
        : await previewViewData(source.id, selectedObject.name, request));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoadingPreview(false);
    }
  }
}

function DockCard({
  title,
  visible,
  children,
  onClose
}: {
  title: string;
  visible: boolean;
  children: React.ReactNode;
  onClose: () => void;
}) {
  if (!visible) return null;

  return (
    <section className="database-center-dock-card">
      <div className="database-center-dock-card__header">
        <Text strong>{title}</Text>
        <Button size="small" theme="borderless" onClick={onClose}>x</Button>
      </div>
      <div className="database-center-dock-card__body">{children}</div>
    </section>
  );
}

function downloadText(content: string, filename: string) {
  const blob = new Blob([content], { type: "text/plain;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

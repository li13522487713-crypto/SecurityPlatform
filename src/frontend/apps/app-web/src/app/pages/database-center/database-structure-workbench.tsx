import { Button, Empty, Space, Spin, Tabs } from "@douyinfe/semi-ui";
import { IconPlus } from "@douyinfe/semi-icons";
import { useState } from "react";
import type {
  DatabaseCenterEnvironment,
  DatabaseCenterObjectSummary,
  DatabaseCenterSchemaStructure,
  DatabaseCenterSourceDetail,
  DatabaseCenterSourceSummary
} from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";
import { CreateTableDrawer } from "./create-table-drawer";
import { CreateViewDrawer } from "./create-view-drawer";
import { DatabaseCenterDock } from "./database-center-dock";
import { ErDiagramCanvas } from "./er-diagram-canvas";
import { ObjectDetailDrawer } from "../database-structure/object-detail-drawer";
import { SqlEditorPanel } from "./sql-editor-panel";
import { TableObjectList } from "./table-object-list";

interface DatabaseStructureWorkbenchProps {
  labels: DatabaseCenterLabels;
  source: DatabaseCenterSourceSummary | DatabaseCenterSourceDetail | null;
  sourceId: string;
  schemaName: string;
  environment: DatabaseCenterEnvironment;
  structure: DatabaseCenterSchemaStructure | null;
  selectedObject: DatabaseCenterObjectSummary | null;
  loading: boolean;
  onSelectObject: (object: DatabaseCenterObjectSummary) => void;
  onStructureChanged: () => Promise<void> | void;
}

export function DatabaseStructureWorkbench({
  labels,
  source,
  sourceId,
  schemaName,
  environment,
  structure,
  selectedObject,
  loading,
  onSelectObject,
  onStructureChanged
}: DatabaseStructureWorkbenchProps) {
  const objects = structure?.objects ?? [];
  const [createTableVisible, setCreateTableVisible] = useState(false);
  const [createViewVisible, setCreateViewVisible] = useState(false);
  const [detailTab, setDetailTab] = useState<"structure" | "preview" | "ddl">("structure");
  const [detailObject, setDetailObject] = useState<DatabaseCenterObjectSummary | null>(null);
  const canEdit = Boolean(sourceId && schemaName && environment === "Draft");
  const drawerObject = detailObject && (detailObject.objectType === "table" || detailObject.objectType === "view")
    ? {
      name: detailObject.name,
      objectType: detailObject.objectType,
      schema: detailObject.schema ?? schemaName,
      rowCount: detailObject.rowCount,
      comment: detailObject.comment,
      engine: detailObject.engine,
      createdAt: detailObject.createdAt,
      updatedAt: detailObject.updatedAt,
      canPreview: detailObject.canPreview ?? true,
      canDrop: detailObject.canDrop ?? true
    }
    : null;

  function openObject(object: DatabaseCenterObjectSummary, action: "structure" | "preview" | "ddl" = "structure") {
    onSelectObject(object);
    setDetailTab(action);
    setDetailObject(object);
  }

  return (
    <main className="database-center-workbench">
      <div className="database-center-workbench__main">
        <Spin spinning={loading}>
          {!sourceId ? (
            <Empty description={labels.noSource} />
          ) : (
            <>
            <Space style={{ justifyContent: "space-between", width: "100%", marginBottom: 12 }}>
              <Space />
              <Space>
                <Button icon={<IconPlus />} disabled={!canEdit} onClick={() => setCreateTableVisible(true)}>{labels.createTableVisual}</Button>
                <Button icon={<IconPlus />} disabled={!canEdit} onClick={() => setCreateViewVisible(true)}>{labels.createView}</Button>
              </Space>
            </Space>
            <Tabs>
              <Tabs.TabPane tab={labels.structure} itemKey="structure">
                <TableObjectList labels={labels} objects={objects} onSelectObject={openObject} />
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.erDiagram} itemKey="er">
                <ErDiagramCanvas labels={labels} structure={structure} onSelectObject={onSelectObject} />
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.sqlEditor} itemKey="sql">
                <SqlEditorPanel labels={labels} sourceId={sourceId} schema={schemaName} environment={environment} />
              </Tabs.TabPane>
            </Tabs>
            </>
          )}
        </Spin>
      </div>
      <DatabaseCenterDock labels={labels} source={source} selectedObject={selectedObject} structure={structure} />
      <ObjectDetailDrawer
        databaseId={sourceId}
        object={drawerObject}
        initialTab={detailTab}
        onClose={() => setDetailObject(null)}
      />
      <CreateTableDrawer
        labels={labels}
        visible={createTableVisible}
        sourceId={sourceId}
        schemaName={schemaName}
        onClose={() => setCreateTableVisible(false)}
        onCreated={onStructureChanged}
      />
      <CreateViewDrawer
        labels={labels}
        visible={createViewVisible}
        sourceId={sourceId}
        schemaName={schemaName}
        onClose={() => setCreateViewVisible(false)}
        onCreated={onStructureChanged}
      />
    </main>
  );
}

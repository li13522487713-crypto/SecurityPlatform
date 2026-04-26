import { Button, Checkbox, Empty, Input, Modal, SideSheet, Space, Spin, Tabs, Toast } from "@douyinfe/semi-ui";
import { IconExpand, IconPlus } from "@douyinfe/semi-icons";
import { useEffect, useState } from "react";
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
import { DataPreviewTable } from "../database-structure/data-preview-table";
import { SqlCodeEditor } from "../database-structure/sql-code-editor";
import {
  dropTable,
  dropView,
  getTableDdl,
  getViewDdl,
  previewTableData,
  previewViewData,
  type PreviewDataResponse
} from "../../../services/api-database-structure";

export type DatabaseCenterObjectAction = "structure" | "preview" | "ddl" | "delete";

export interface DatabaseCenterObjectRequest {
  token: number;
  object: DatabaseCenterObjectSummary;
  action: DatabaseCenterObjectAction;
}

interface DatabaseStructureWorkbenchProps {
  labels: DatabaseCenterLabels;
  source: DatabaseCenterSourceSummary | DatabaseCenterSourceDetail | null;
  sourceId: string;
  schemaName: string;
  environment: DatabaseCenterEnvironment;
  structure: DatabaseCenterSchemaStructure | null;
  selectedObject: DatabaseCenterObjectSummary | null;
  objectRequest?: DatabaseCenterObjectRequest | null;
  loading: boolean;
  fullscreen?: boolean;
  onSelectObject: (object: DatabaseCenterObjectSummary) => void;
  onStructureChanged: () => Promise<void> | void;
  onToggleFullscreen?: () => void;
}

export function DatabaseStructureWorkbench({
  labels,
  source,
  sourceId,
  schemaName,
  environment,
  structure,
  selectedObject,
  objectRequest,
  loading,
  fullscreen = false,
  onSelectObject,
  onStructureChanged,
  onToggleFullscreen
}: DatabaseStructureWorkbenchProps) {
  const objects = structure?.objects ?? [];
  const tables = objects.filter(item => item.objectType === "table");
  const views = objects.filter(item => item.objectType === "view");
  const procedures = objects.filter(item => item.objectType === "procedure" || item.objectType === "function");
  const triggers = objects.filter(item => item.objectType === "trigger" || item.objectType === "event");
  const [createTableVisible, setCreateTableVisible] = useState(false);
  const [createViewVisible, setCreateViewVisible] = useState(false);
  const [detailTab, setDetailTab] = useState<"structure" | "preview" | "ddl">("structure");
  const [detailObject, setDetailObject] = useState<DatabaseCenterObjectSummary | null>(null);
  const [workbenchTab, setWorkbenchTab] = useState("er");
  const [dockVisible, setDockVisible] = useState(false);
  const [inlinePreview, setInlinePreview] = useState<PreviewDataResponse | null>(null);
  const [inlineDdl, setInlineDdl] = useState("");
  const [inlineLoading, setInlineLoading] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<DatabaseCenterObjectSummary | null>(null);
  const [confirmName, setConfirmName] = useState("");
  const [confirmDanger, setConfirmDanger] = useState(false);
  const [deleting, setDeleting] = useState(false);
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
    if (action === "preview") {
      setWorkbenchTab("preview");
      void loadInlinePreview(object);
    }
    if (action === "ddl") {
      setWorkbenchTab("ddl");
      void loadInlineDdl(object);
    }
  }

  function requestObjectAction(object: DatabaseCenterObjectSummary, action: DatabaseCenterObjectAction = "structure") {
    if (action === "delete") {
      setDeleteTarget(object);
      setConfirmName("");
      setConfirmDanger(false);
      onSelectObject(object);
      return;
    }

    openObject(object, action);
  }

  useEffect(() => {
    if (!objectRequest) return;
    requestObjectAction(objectRequest.object, objectRequest.action);
  }, [objectRequest?.token]);

  return (
    <main className={`database-center-workbench${fullscreen ? " database-center-workbench--fullscreen" : ""}`}>
      <div className="database-center-workbench__main">
        <Spin spinning={loading}>
          {!sourceId ? (
            <Empty description={labels.noSource} />
          ) : (
            <>
            <Space className="database-center-workbench__toolbar" style={{ width: "100%", marginBottom: 12 }}>
              <Space />
              <Space wrap>
                <Button className="database-center-dock-open-button" onClick={() => setDockVisible(true)}>{labels.globalActions}</Button>
                <Button icon={<IconExpand />} onClick={onToggleFullscreen}>{fullscreen ? labels.exitFullscreen : labels.fullscreen}</Button>
                <Button icon={<IconPlus />} disabled={!canEdit} onClick={() => setCreateTableVisible(true)}>{labels.createTableVisual}</Button>
                <Button icon={<IconPlus />} disabled={!canEdit} onClick={() => setCreateViewVisible(true)}>{labels.createView}</Button>
              </Space>
            </Space>
            <Tabs className="database-center-workbench-tabs" activeKey={workbenchTab} onChange={key => setWorkbenchTab(String(key))}>
              <Tabs.TabPane tab={labels.erDiagram} itemKey="er">
                <ErDiagramCanvas labels={labels} structure={structure} onSelectObject={object => requestObjectAction(object, "structure")} />
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.tableList} itemKey="tables">
                <TableObjectList labels={labels} objects={tables} onSelectObject={openObject} onDeleteObject={setDeleteTarget} />
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.viewList} itemKey="views">
                <TableObjectList labels={labels} objects={views} onSelectObject={openObject} onDeleteObject={setDeleteTarget} />
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.storedProcedures} itemKey="procedures">
                <TableObjectList labels={labels} objects={procedures} onSelectObject={openObject} />
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.triggers} itemKey="triggers">
                <TableObjectList labels={labels} objects={triggers} onSelectObject={openObject} />
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.dataPreview} itemKey="preview">
                <Spin spinning={inlineLoading}>
                  {inlinePreview ? <DataPreviewTable data={inlinePreview} /> : <Empty description={labels.noObjectSelected} />}
                </Spin>
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.ddl} itemKey="ddl">
                <Spin spinning={inlineLoading}>
                  {inlineDdl ? <SqlCodeEditor value={inlineDdl} readOnly height={fullscreen ? "calc(100dvh - 170px)" : 420} /> : <Empty description={labels.noObjectSelected} />}
                </Spin>
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.sqlEditor} itemKey="sql">
                <SqlEditorPanel labels={labels} sourceId={sourceId} schema={schemaName} environment={environment} fullscreen={fullscreen} />
              </Tabs.TabPane>
            </Tabs>
            </>
          )}
        </Spin>
      </div>
      <DatabaseCenterDock
        labels={labels}
        source={source}
        selectedObject={selectedObject}
        structure={structure}
        onSelectObject={object => requestObjectAction(object, "structure")}
      />
      <SideSheet
        visible={dockVisible}
        onCancel={() => setDockVisible(false)}
        title={labels.globalActions}
        width="min(980px, calc(100vw - 32px))"
        bodyStyle={{ padding: 0, overflow: "hidden" }}
      >
        <div className="database-center-dock-drawer">
          <DatabaseCenterDock
            labels={labels}
            source={source}
            selectedObject={selectedObject}
            structure={structure}
            onSelectObject={object => requestObjectAction(object, "structure")}
          />
        </div>
      </SideSheet>
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
      <Modal
        visible={Boolean(deleteTarget)}
        title={labels.deleteConfirmTitle}
        okText={labels.delete}
        cancelText={labels.cancel}
        okType="danger"
        okButtonProps={{ disabled: !deleteTarget || confirmName !== deleteTarget.name || !confirmDanger, loading: deleting }}
        onCancel={() => setDeleteTarget(null)}
        onOk={() => void confirmDelete()}
      >
        <Space vertical align="start" style={{ width: "100%" }}>
          <span>{labels.deleteConfirmContent}</span>
          <strong>{deleteTarget?.name}</strong>
          <Input value={confirmName} placeholder={deleteTarget?.name} onChange={setConfirmName} />
          <Checkbox checked={confirmDanger} onChange={event => setConfirmDanger(Boolean(event.target.checked))}>
            {labels.dangerConfirm}
          </Checkbox>
        </Space>
      </Modal>
    </main>
  );

  async function loadInlinePreview(object = selectedObject) {
    if (!sourceId || !object || (object.objectType !== "table" && object.objectType !== "view")) {
      setInlinePreview(null);
      return;
    }

    setInlineLoading(true);
    try {
      const request = { schema: object.schema ?? schemaName, pageIndex: 1, pageSize: 20 };
      setInlinePreview(object.objectType === "view"
        ? await previewViewData(sourceId, object.name, request)
        : await previewTableData(sourceId, object.name, request));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setInlineLoading(false);
    }
  }

  async function loadInlineDdl(object = selectedObject) {
    if (!sourceId || !object || (object.objectType !== "table" && object.objectType !== "view")) {
      setInlineDdl("");
      return;
    }

    setInlineLoading(true);
    try {
      const result = object.objectType === "view"
        ? await getViewDdl(sourceId, object.name, object.schema ?? schemaName)
        : await getTableDdl(sourceId, object.name, object.schema ?? schemaName);
      setInlineDdl(result.ddl);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setInlineLoading(false);
    }
  }

  async function confirmDelete() {
    if (!deleteTarget || !canEdit) return;

    setDeleting(true);
    try {
      const request = { schema: deleteTarget.schema ?? schemaName, confirmName: deleteTarget.name, confirmDanger: true };
      if (deleteTarget.objectType === "view") {
        await dropView(sourceId, deleteTarget.name, request);
      } else if (deleteTarget.objectType === "table") {
        await dropTable(sourceId, deleteTarget.name, request);
      }
      Toast.success(labels.saveSuccess);
      setDeleteTarget(null);
      await onStructureChanged();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setDeleting(false);
    }
  }
}

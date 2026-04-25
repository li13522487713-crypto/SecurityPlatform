import { Empty, Spin, Tabs } from "@douyinfe/semi-ui";
import type {
  DatabaseCenterEnvironment,
  DatabaseCenterObjectSummary,
  DatabaseCenterSchemaStructure,
  DatabaseCenterSourceDetail,
  DatabaseCenterSourceSummary
} from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";
import { DatabaseCenterDock } from "./database-center-dock";
import { ErDiagramCanvas } from "./er-diagram-canvas";
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
  onSelectObject
}: DatabaseStructureWorkbenchProps) {
  const objects = structure?.objects ?? [];

  return (
    <main className="database-center-workbench">
      <div className="database-center-workbench__main">
        <Spin spinning={loading}>
          {!sourceId ? (
            <Empty description={labels.noSource} />
          ) : (
            <Tabs>
              <Tabs.TabPane tab={labels.structure} itemKey="structure">
                <TableObjectList labels={labels} objects={objects} onSelectObject={onSelectObject} />
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.erDiagram} itemKey="er">
                <ErDiagramCanvas labels={labels} structure={structure} onSelectObject={onSelectObject} />
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.sqlEditor} itemKey="sql">
                <SqlEditorPanel labels={labels} sourceId={sourceId} schema={schemaName} environment={environment} />
              </Tabs.TabPane>
            </Tabs>
          )}
        </Spin>
      </div>
      <DatabaseCenterDock labels={labels} source={source} selectedObject={selectedObject} structure={structure} />
    </main>
  );
}

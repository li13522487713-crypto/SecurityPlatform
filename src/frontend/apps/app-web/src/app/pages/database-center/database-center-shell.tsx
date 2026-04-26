import { useMemo, useState } from "react";
import { Button, Space, Tag, Typography } from "@douyinfe/semi-ui";
import { IconDownloadStroked, IconPlus, IconRefresh, IconSetting } from "@douyinfe/semi-icons";
import type { DatabaseCenterObjectSummary } from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";
import { AiDatabaseCreateWizard } from "./ai-database-create-wizard";
import { DatabaseSchemaTree } from "./database-schema-tree";
import { DatabaseSourcePanel } from "./database-source-panel";
import { DatabaseStructureWorkbench, type DatabaseCenterObjectAction, type DatabaseCenterObjectRequest } from "./database-structure-workbench";
import { HostProfileManageDrawer } from "./host-profile-manage-drawer";
import { ImportDdlDrawer } from "./import-ddl-drawer";
import { InstanceDetailPanel } from "./instance-detail-panel";
import { useDatabaseCenter } from "./use-database-center";

const { Text, Title } = Typography;

interface DatabaseCenterShellProps {
  labels: DatabaseCenterLabels;
  workspaceId?: string;
  initialSourceId?: string;
}

export function DatabaseCenterShell({ labels, workspaceId, initialSourceId }: DatabaseCenterShellProps) {
  const state = useDatabaseCenter({ workspaceId, initialSourceId, labels });
  const [hostProfilesVisible, setHostProfilesVisible] = useState(false);
  const [createVisible, setCreateVisible] = useState(false);
  const [importDdlVisible, setImportDdlVisible] = useState(false);
  const [selectedObject, setSelectedObject] = useState<DatabaseCenterObjectSummary | null>(null);
  const [objectRequest, setObjectRequest] = useState<DatabaseCenterObjectRequest | null>(null);

  const selectedColumns = useMemo(() => {
    if (!selectedObject || !state.structure) return [];
    return state.structure.columnsByObject[selectedObject.id] ?? state.structure.columnsByObject[selectedObject.name] ?? [];
  }, [selectedObject, state.structure]);

  function selectSource(id: string) {
    state.setSelectedSourceId(id);
    state.setSelectedSchema("");
    setSelectedObject(null);
  }

  function requestObjectAction(object: DatabaseCenterObjectSummary, action: DatabaseCenterObjectAction = "structure") {
    setSelectedObject(object);
    setObjectRequest({ token: Date.now(), object, action });
  }

  return (
    <div className="database-center-page">
      <header className="database-center-header">
        <Space vertical align="start" spacing={4}>
          <Space>
            <Title heading={3} style={{ margin: 0 }}>{labels.title}</Title>
            {state.selectedSource ? <Tag color="blue">{state.selectedSource.driverCode}</Tag> : null}
            {state.environment ? <Tag color={state.environment === "Draft" ? "green" : "orange"}>{state.environment}</Tag> : null}
          </Space>
          <Text type="tertiary">{labels.subtitle}</Text>
        </Space>
        <Space>
          <Tag color={state.selectedSource?.provisionState === "Ready" ? "green" : "orange"}>
            {state.selectedSource?.provisionState === "Ready" ? labels.connected : state.selectedSource?.provisionState ?? "Pending"}
          </Tag>
          <Button icon={<IconRefresh />} onClick={() => void state.refresh()}>{labels.refresh}</Button>
          <Button icon={<IconDownloadStroked />} disabled={!state.selectedSourceId || !state.selectedSchema || state.environment !== "Draft"} onClick={() => setImportDdlVisible(true)}>{labels.importDdl}</Button>
          <Button icon={<IconSetting />} onClick={() => setHostProfilesVisible(true)}>{labels.hostProfiles}</Button>
          <Button icon={<IconPlus />} theme="solid" onClick={() => setCreateVisible(true)}>{labels.newDatabase}</Button>
        </Space>
      </header>
      <section className="database-center-guide">
        {[
          ["1", labels.sources, labels.guideSourcesHint],
          ["2", labels.schemas, labels.guideSchemasHint],
          ["3", labels.structure, labels.guideStructureHint],
          ["4", labels.details, labels.guideDetailsHint],
          ["5", labels.globalActions, labels.guideGlobalActionsHint]
        ].map(item => (
          <div key={item[0]} className="database-center-guide-card">
            <span>{item[0]}</span>
            <strong>{item[1]}</strong>
            <Text type="tertiary" size="small">{item[2]}</Text>
          </div>
        ))}
      </section>
      <section className="database-center-layout">
        <nav className="database-center-nav">
          {[
            labels.overview,
            labels.dataSourceManagement,
            labels.aiDatabases,
            labels.structure,
            labels.sqlEditor,
            labels.backupManagement,
            labels.auditLogs,
            labels.settingsCenter
          ].map(item => (
            <button
              key={item}
              type="button"
              className={`database-center-nav__item${item === labels.dataSourceManagement ? " database-center-nav__item--active" : ""}`}
            >
              <span className="database-center-nav__glyph" />
              {item}
            </button>
          ))}
        </nav>
        <div className="database-center-shell">
        <DatabaseSourcePanel
          labels={labels}
          sources={state.sources}
          selectedSourceId={state.selectedSourceId}
          keyword={state.keyword}
          loading={state.loadingSources}
          onKeywordChange={state.setKeyword}
          onSelectSource={selectSource}
          onRefresh={() => void state.loadSources()}
          onOpenCreate={() => setCreateVisible(true)}
          onOpenHostProfiles={() => setHostProfilesVisible(true)}
        />
        <DatabaseSchemaTree
          labels={labels}
          environment={state.environment}
          schemas={state.schemas}
          selectedSchema={state.selectedSchema}
          structure={state.structure}
          loading={state.loadingStructure}
          onEnvironmentChange={value => {
            state.setEnvironment(value);
            setSelectedObject(null);
          }}
          onSchemaChange={value => {
            state.setSelectedSchema(value);
            setSelectedObject(null);
          }}
          onObjectSelect={requestObjectAction}
        />
        <DatabaseStructureWorkbench
          labels={labels}
          source={state.selectedSource}
          sourceId={state.selectedSourceId}
          schemaName={state.selectedSchema}
          environment={state.environment}
          structure={state.structure}
          selectedObject={selectedObject}
          objectRequest={objectRequest}
          loading={state.loadingStructure}
          onSelectObject={setSelectedObject}
          onStructureChanged={state.loadStructure}
        />
        <InstanceDetailPanel
          labels={labels}
          source={state.sourceDetail ?? state.selectedSource}
          selectedObject={selectedObject}
          columns={selectedColumns}
          onObjectAction={requestObjectAction}
          onRefresh={() => void state.refresh()}
        />
        </div>
      </section>
      <HostProfileManageDrawer
        labels={labels}
        visible={hostProfilesVisible}
        onClose={() => setHostProfilesVisible(false)}
      />
      <AiDatabaseCreateWizard
        labels={labels}
        workspaceId={workspaceId}
        visible={createVisible}
        onClose={() => setCreateVisible(false)}
        onCreated={async id => {
          selectSource(id);
          await state.refresh();
        }}
      />
      <ImportDdlDrawer
        labels={labels}
        visible={importDdlVisible}
        sourceId={state.selectedSourceId}
        schemaName={state.selectedSchema}
        canEdit={Boolean(state.selectedSourceId && state.selectedSchema && state.environment === "Draft")}
        onClose={() => setImportDdlVisible(false)}
        onImported={state.loadStructure}
      />
    </div>
  );
}

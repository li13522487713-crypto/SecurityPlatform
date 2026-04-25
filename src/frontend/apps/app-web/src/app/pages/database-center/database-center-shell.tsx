import { useMemo, useState } from "react";
import { Button, Space, Tag, Typography } from "@douyinfe/semi-ui";
import { IconPlus, IconRefresh, IconSetting } from "@douyinfe/semi-icons";
import type { DatabaseCenterObjectSummary } from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";
import { AiDatabaseCreateWizard } from "./ai-database-create-wizard";
import { DatabaseSchemaTree } from "./database-schema-tree";
import { DatabaseSourcePanel } from "./database-source-panel";
import { DatabaseStructureWorkbench } from "./database-structure-workbench";
import { HostProfileManageDrawer } from "./host-profile-manage-drawer";
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
  const [selectedObject, setSelectedObject] = useState<DatabaseCenterObjectSummary | null>(null);

  const selectedColumns = useMemo(() => {
    if (!selectedObject || !state.structure) return [];
    return state.structure.columnsByObject[selectedObject.id] ?? state.structure.columnsByObject[selectedObject.name] ?? [];
  }, [selectedObject, state.structure]);

  function selectSource(id: string) {
    state.setSelectedSourceId(id);
    state.setSelectedSchema("");
    setSelectedObject(null);
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
          <Button icon={<IconRefresh />} onClick={() => void state.refresh()}>{labels.refresh}</Button>
          <Button icon={<IconSetting />} onClick={() => setHostProfilesVisible(true)}>{labels.hostProfiles}</Button>
          <Button icon={<IconPlus />} theme="solid" onClick={() => setCreateVisible(true)}>{labels.newDatabase}</Button>
        </Space>
      </header>
      <section className="database-center-shell">
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
          onObjectSelect={setSelectedObject}
        />
        <DatabaseStructureWorkbench
          labels={labels}
          source={state.selectedSource}
          sourceId={state.selectedSourceId}
          schemaName={state.selectedSchema}
          environment={state.environment}
          structure={state.structure}
          selectedObject={selectedObject}
          loading={state.loadingStructure}
          onSelectObject={setSelectedObject}
          onStructureChanged={state.loadStructure}
        />
        <InstanceDetailPanel
          labels={labels}
          source={state.sourceDetail ?? state.selectedSource}
          selectedObject={selectedObject}
          columns={selectedColumns}
        />
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
    </div>
  );
}

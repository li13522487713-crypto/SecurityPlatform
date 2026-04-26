import { useMemo, useState } from "react";
import { Button, SideSheet, Space, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconDownloadStroked, IconPlus, IconRefresh, IconSetting } from "@douyinfe/semi-icons";
import {
  ResponsivePageFrame,
  ResponsiveSummaryCards,
  ResponsiveToolbar,
  useResponsiveBreakpoint
} from "../../_shared";
import type { AiWorkspaceLibraryItem } from "../../../services/api-ai-workspace";
import type { DatabaseCenterObjectSummary, DatabaseCenterSourceSummary } from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";
import { AiDatabaseCreateWizard } from "./ai-database-create-wizard";
import { DatabaseSchemaTree } from "./database-schema-tree";
import { DatabaseSourcePanel } from "./database-source-panel";
import { DatabaseStructureWorkbench, type DatabaseCenterObjectAction, type DatabaseCenterObjectRequest } from "./database-structure-workbench";
import { HostProfileManageDrawer } from "./host-profile-manage-drawer";
import { ImportDdlDrawer } from "./import-ddl-drawer";
import { InstanceDetailPanel } from "./instance-detail-panel";
import { MigrationWizardDrawer } from "../components/migration-wizard/migration-wizard-drawer";
import { useDatabaseCenter } from "./use-database-center";

const { Text, Title } = Typography;
type DatabaseCenterDrawerPanel = "guide" | "sources" | "schemas" | "details";
const SETUP_CONSOLE_TOKEN_KEY = "atlas_setup_console_token";

interface DatabaseCenterShellProps {
  labels: DatabaseCenterLabels;
  workspaceId?: string;
  initialSourceId?: string;
}

export function DatabaseCenterShell({ labels, workspaceId, initialSourceId }: DatabaseCenterShellProps) {
  const { ref: frameRef, breakpoint, size } = useResponsiveBreakpoint<HTMLDivElement>();
  const state = useDatabaseCenter({ workspaceId, initialSourceId, labels });
  const [hostProfilesVisible, setHostProfilesVisible] = useState(false);
  const [createVisible, setCreateVisible] = useState(false);
  const [importDdlVisible, setImportDdlVisible] = useState(false);
  const [drawerPanel, setDrawerPanel] = useState<DatabaseCenterDrawerPanel | null>(null);
  const [migrationSource, setMigrationSource] = useState<AiWorkspaceLibraryItem | null>(null);
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
    if (breakpoint !== "wide") {
      setDrawerPanel(null);
    }
  }

  function openMigration(source: DatabaseCenterSourceSummary) {
    if (!source.aiDatabaseId) {
      return;
    }

    if (!hasSetupConsoleToken()) {
      Toast.warning(labels.migrationAuthRequired);
      return;
    }

    setMigrationSource({
      resourceType: "database",
      resourceId: Number(source.aiDatabaseId),
      name: source.name,
      description: source.description ?? undefined,
      updatedAt: source.updatedAt ?? source.createdAt ?? new Date().toISOString(),
      path: workspaceId ? `/space/${workspaceId}/database/${source.aiDatabaseId}/structure` : "",
      subType: "table",
      typeLabel: labels.aiDatabases,
      source: "custom",
      status: source.provisionState ?? source.status ?? undefined
    });
  }

  const guideCards = [
    ["1", labels.sources, labels.guideSourcesHint],
    ["2", labels.schemas, labels.guideSchemasHint],
    ["3", labels.structure, labels.guideStructureHint],
    ["4", labels.details, labels.guideDetailsHint],
    ["5", labels.globalActions, labels.guideGlobalActionsHint]
  ];

  const drawerTitle = drawerPanel === "guide"
    ? labels.overview
    : drawerPanel === "sources"
      ? labels.sources
      : drawerPanel === "schemas"
        ? labels.schemas
        : labels.details;

  return (
    <ResponsivePageFrame
      containerRef={frameRef}
      breakpoint={breakpoint}
      className={[
        "database-center-page",
        `database-center-page--${breakpoint}`,
        size.height > 0 && size.height < 760 ? "database-center-page--low-height" : ""
      ].filter(Boolean).join(" ")}
      header={
        <>
      <header className="database-center-header">
        <ResponsiveToolbar
          main={
            <Space vertical align="start" spacing={4} className="database-center-title-block">
              <Space wrap>
                <Title heading={3} style={{ margin: 0 }}>{labels.title}</Title>
                {state.selectedSource ? <Tag color="blue">{state.selectedSource.driverCode}</Tag> : null}
                {state.environment ? <Tag color={state.environment === "Draft" ? "green" : "orange"}>{state.environment}</Tag> : null}
              </Space>
              <Text type="tertiary" ellipsis={{ showTooltip: true }}>{labels.subtitle}</Text>
            </Space>
          }
          actions={
            <>
              <Tag color={state.selectedSource?.provisionState === "Ready" ? "green" : "orange"}>
                {state.selectedSource?.provisionState === "Ready" ? labels.connected : state.selectedSource?.provisionState ?? "Pending"}
              </Tag>
              <Button icon={<IconRefresh />} onClick={() => void state.refresh()}>{labels.refresh}</Button>
              <Button icon={<IconDownloadStroked />} disabled={!state.selectedSourceId || !state.selectedSchema || state.environment !== "Draft"} onClick={() => setImportDdlVisible(true)}>{labels.importDdl}</Button>
              <Button icon={<IconSetting />} onClick={() => setHostProfilesVisible(true)}>{labels.hostProfiles}</Button>
              <Button icon={<IconPlus />} theme="solid" onClick={() => setCreateVisible(true)}>{labels.newDatabase}</Button>
            </>
          }
        />
      </header>
      <ResponsiveSummaryCards className="database-center-guide" minCardWidth={220}>
        {guideCards.map(item => (
          <div key={item[0]} className="database-center-guide-card">
            <span className="database-center-guide-card__index">{item[0]}</span>
            <div className="database-center-guide-card__content">
              <strong>{item[1]}</strong>
              <Text type="tertiary" size="small">{item[2]}</Text>
            </div>
          </div>
        ))}
      </ResponsiveSummaryCards>
        </>
      }
    >
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
        <div className="database-center-drawer-actions">
          <Button size="small" onClick={() => setDrawerPanel("guide")}>{labels.overview}</Button>
          <Button size="small" onClick={() => setDrawerPanel("sources")}>{labels.sources}</Button>
          <Button size="small" onClick={() => setDrawerPanel("schemas")} disabled={!state.selectedSourceId}>{labels.schemas}</Button>
          <Button size="small" onClick={() => setDrawerPanel("details")}>{labels.details}</Button>
        </div>
        <div className="database-center-shell">
        {renderSourcePanel()}
        {renderSchemaPanel()}
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
        {renderDetailPanel()}
        </div>
      </section>
      <SideSheet
        visible={Boolean(drawerPanel)}
        onCancel={() => setDrawerPanel(null)}
        title={drawerTitle}
        width="min(420px, calc(100vw - 32px))"
        bodyStyle={{ padding: 0, overflow: "hidden" }}
      >
        <div className="database-center-responsive-drawer">
          {drawerPanel === "guide" ? (
            <ResponsiveSummaryCards className="database-center-guide database-center-guide--drawer" minCardWidth={220}>
              {guideCards.map(item => (
                <div key={item[0]} className="database-center-guide-card">
                  <span className="database-center-guide-card__index">{item[0]}</span>
                  <div className="database-center-guide-card__content">
                    <strong>{item[1]}</strong>
                    <Text type="tertiary" size="small">{item[2]}</Text>
                  </div>
                </div>
              ))}
            </ResponsiveSummaryCards>
          ) : null}
          {drawerPanel === "sources" ? renderSourcePanel() : null}
          {drawerPanel === "schemas" ? renderSchemaPanel() : null}
          {drawerPanel === "details" ? renderDetailPanel() : null}
        </div>
      </SideSheet>
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
      <MigrationWizardDrawer
        visible={Boolean(migrationSource)}
        source={migrationSource}
        onClose={() => setMigrationSource(null)}
        onTargetCreated={() => void state.refresh()}
      />
    </ResponsivePageFrame>
  );

  function renderSourcePanel() {
    return (
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
        onOpenMigration={openMigration}
      />
    );
  }

  function renderSchemaPanel() {
    return (
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
    );
  }

  function renderDetailPanel() {
    return (
      <InstanceDetailPanel
        labels={labels}
        source={state.sourceDetail ?? state.selectedSource}
        selectedObject={selectedObject}
        columns={selectedColumns}
        onObjectAction={requestObjectAction}
        onRefresh={() => void state.refresh()}
      />
    );
  }
}

function hasSetupConsoleToken(): boolean {
  if (typeof window === "undefined") {
    return false;
  }

  try {
    return Boolean(window.sessionStorage.getItem(SETUP_CONSOLE_TOKEN_KEY));
  } catch {
    return false;
  }
}

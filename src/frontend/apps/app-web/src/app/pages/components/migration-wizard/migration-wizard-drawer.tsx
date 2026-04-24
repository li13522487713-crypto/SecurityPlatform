import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import {
  Banner,
  Button,
  Card,
  Checkbox,
  Input,
  InputNumber,
  Progress,
  Select,
  SideSheet,
  Space,
  Steps,
  Table,
  Tabs,
  Toast,
  Tooltip,
  Typography
} from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconRefresh } from "@douyinfe/semi-icons";
import { useAppI18n } from "../../../i18n";
import type {
  DataMigrationJobDto,
  DataMigrationPrecheckResultDto,
  DataMigrationProgressDto,
  DataMigrationReportDto,
  DataMigrationTableProgressDto,
  DbConnectionConfig
} from "../../../../services/api-setup-console";
import { setupConsoleApi } from "../../../../services/api-setup-console";
import {
  formatTenantDataSourceSummary,
  listTenantDataSources,
  testTenantDataSourceById,
  type TenantDataSourceDto
} from "../../../../services/api-tenant-datasource";
import type { AiWorkspaceLibraryItem } from "../../../../services/api-ai-workspace";
import { CreateDataSourceModal } from "../create-datasource-modal";

const { Text, Title } = Typography;

type WriteMode = "InsertOnly" | "TruncateThenInsert" | "Upsert";

export interface MigrationWizardDrawerProps {
  visible: boolean;
  source: AiWorkspaceLibraryItem | null;
  onClose: () => void;
}

interface MigrationConfigState {
  scopeMode: "all" | "module" | "table";
  selectedTablesText: string;
  batchSize: number;
  writeMode: WriteMode;
  createSchema: boolean;
  migrateSystemTables: boolean;
  migrateFiles: boolean;
  validateAfterCopy: boolean;
}

const DEFAULT_CONFIG: MigrationConfigState = {
  scopeMode: "all",
  selectedTablesText: "",
  batchSize: 10000,
  writeMode: "InsertOnly",
  createSchema: true,
  migrateSystemTables: false,
  migrateFiles: false,
  validateAfterCopy: false
};

interface ApiFailureShape {
  message?: string | null;
  traceId?: string | null;
  code?: string | null;
  errorCode?: string | null;
}

function isConsoleAuthExpired(response: ApiFailureShape): boolean {
  const code = String(response.code ?? response.errorCode ?? "").toUpperCase();
  return code === "CONSOLE_TOKEN_EXPIRED" || response.message?.includes("console token") === true;
}

function apiMessage(response: ApiFailureShape, authExpiredMessage: string): string {
  if (isConsoleAuthExpired(response)) {
    return authExpiredMessage;
  }

  const message = response.message?.trim() || "请求失败";
  return response.traceId ? `${message} (traceId: ${response.traceId})` : message;
}

function normalizedMigrationState(state?: string | null): string {
  return String(state ?? "").toLowerCase();
}

function isTerminalState(state?: string | null): boolean {
  return [
    "succeeded",
    "failed",
    "cancelled",
    "validation_failed",
    "validated",
    "cutover_completed",
    "cutover-completed",
    "cutover_failed",
    "cutover-failed"
  ].includes(normalizedMigrationState(state));
}

function isReadyForValidation(state?: string | null): boolean {
  return [
    "succeeded",
    "validated",
    "validation_failed",
    "cutover_ready",
    "cutover-ready"
  ].includes(normalizedMigrationState(state));
}

function formatTime(value?: string | null): string {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString();
}

function parseTables(text: string): string[] | null {
  const tables = text
    .split(/[\n,]/u)
    .map(item => item.trim())
    .filter(Boolean);
  return tables.length > 0 ? tables : null;
}

function buildSourceConfig(source: AiWorkspaceLibraryItem): DbConnectionConfig {
  return {
    driverCode: "",
    dbType: "",
    mode: "CurrentSystemAiDatabase",
    displayName: source.name,
    aiDatabaseId: Number(source.resourceId)
  };
}

function buildTargetConfig(target: TenantDataSourceDto): DbConnectionConfig {
  return {
    driverCode: target.driverCode || target.dbType,
    dbType: target.dbType,
    mode: "SavedDataSource",
    dataSourceId: Number(target.id),
    displayName: target.name
  };
}

function ConfigField({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label style={{ display: "flex", flexDirection: "column", gap: 6, width: "100%" }}>
      <Text strong>{label}</Text>
      {children}
    </label>
  );
}

export function MigrationWizardDrawer({ visible, source, onClose }: MigrationWizardDrawerProps) {
  const { t } = useAppI18n();
  const [step, setStep] = useState(0);
  const [dataSources, setDataSources] = useState<TenantDataSourceDto[]>([]);
  const [targetId, setTargetId] = useState<string | null>(null);
  const [loadingTargets, setLoadingTargets] = useState(false);
  const [targetKeyword, setTargetKeyword] = useState("");
  const [createDataSourceOpen, setCreateDataSourceOpen] = useState(false);
  const [config, setConfig] = useState<MigrationConfigState>(DEFAULT_CONFIG);
  const [job, setJob] = useState<DataMigrationJobDto | null>(null);
  const [precheck, setPrecheck] = useState<DataMigrationPrecheckResultDto | null>(null);
  const [progress, setProgress] = useState<DataMigrationProgressDto | null>(null);
  const [report, setReport] = useState<DataMigrationReportDto | null>(null);
  const [cutoverConfirmed, setCutoverConfirmed] = useState(false);
  const [restartConfirmed, setRestartConfirmed] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);

  const selectedTarget = useMemo(
    () => dataSources.find(item => item.id === targetId) ?? null,
    [dataSources, targetId]
  );

  const filteredTargets = useMemo(() => {
    const keyword = targetKeyword.trim().toLowerCase();
    if (!keyword) {
      return dataSources;
    }
    return dataSources.filter(item =>
      [item.name, item.dbType, item.host, item.databaseName]
        .filter(Boolean)
        .some(value => String(value).toLowerCase().includes(keyword))
    );
  }, [dataSources, targetKeyword]);

  const sourceConfig = useMemo(() => (source ? buildSourceConfig(source) : null), [source]);
  const targetConfig = useMemo(() => (selectedTarget ? buildTargetConfig(selectedTarget) : null), [selectedTarget]);
  const currentMigrationState = progress?.state ?? job?.state ?? null;
  const canEnterValidateStep = isReadyForValidation(currentMigrationState);

  const formatApiMessage = useCallback(
    (response: ApiFailureShape) => apiMessage(response, t("setupConsoleMigrationAuthExpired")),
    [t]
  );

  const resetJobState = useCallback(() => {
    setJob(null);
    setPrecheck(null);
    setProgress(null);
    setReport(null);
    setCutoverConfirmed(false);
    setRestartConfirmed(false);
  }, []);

  const updateConfig = useCallback(
    (updater: (current: MigrationConfigState) => MigrationConfigState) => {
      resetJobState();
      setConfig(updater);
    },
    [resetJobState]
  );

  const handleTargetSelect = useCallback(
    (selected: (string | number)[]) => {
      resetJobState();
      setTargetId(String(selected[0] ?? ""));
    },
    [resetJobState]
  );

  const reset = useCallback(() => {
    setStep(0);
    setTargetId(null);
    setTargetKeyword("");
    setConfig(DEFAULT_CONFIG);
    setJob(null);
    setPrecheck(null);
    setProgress(null);
    setReport(null);
    setCutoverConfirmed(false);
    setRestartConfirmed(false);
    setBusy(null);
  }, []);

  const loadTargets = useCallback(async () => {
    setLoadingTargets(true);
    try {
      const items = await listTenantDataSources();
      setDataSources(items.filter(item => item.isActive));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("cozeLibraryQueryFailed"));
    } finally {
      setLoadingTargets(false);
    }
  }, [t]);

  useEffect(() => {
    if (!visible) {
      reset();
      return;
    }
    void loadTargets();
  }, [loadTargets, reset, visible]);

  useEffect(() => {
    if (!visible || !job || step !== 3) {
      return;
    }

    let stopped = false;
    const poll = async () => {
      const response = await setupConsoleApi.getMigrationProgress(job.id);
      if (stopped) {
        return;
      }
      if (response.success && response.data) {
        setProgress(response.data);
        if (!isTerminalState(response.data.state)) {
          window.setTimeout(() => void poll(), 1500);
        }
      } else {
        Toast.error(formatApiMessage(response));
      }
    };
    void poll();
    return () => {
      stopped = true;
    };
  }, [formatApiMessage, job, step, visible]);

  const guarded = useCallback(async (label: string, action: () => Promise<void>) => {
    if (busy) {
      return;
    }
    setBusy(label);
    try {
      await action();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("cozeLibraryQueryFailed"));
    } finally {
      setBusy(null);
    }
  }, [busy, t]);

  const ensureJob = useCallback(async (): Promise<DataMigrationJobDto> => {
    if (job) {
      return job;
    }
    if (!sourceConfig || !targetConfig) {
      throw new Error(t("setupConsoleMigrationTargetRequired"));
    }

    const response = await setupConsoleApi.createMigrationJob({
      source: sourceConfig,
      target: targetConfig,
      mode: "structure-plus-data",
      moduleScope: { categories: ["all"] },
      allowReExecute: true,
      selectedTables: config.scopeMode === "table" ? parseTables(config.selectedTablesText) : null,
      batchSize: config.batchSize,
      writeMode: config.writeMode,
      createSchema: config.createSchema,
      migrateSystemTables: config.migrateSystemTables,
      migrateFiles: config.migrateFiles,
      validateAfterCopy: config.validateAfterCopy
    });
    if (!response.success || !response.data) {
      throw new Error(formatApiMessage(response));
    }
    setJob(response.data);
    return response.data;
  }, [config, formatApiMessage, job, sourceConfig, targetConfig, t]);

  const handlePrecheck = () =>
    guarded("precheck", async () => {
      const currentJob = await ensureJob();
      const response = await setupConsoleApi.precheckMigrationJob(currentJob.id);
      if (!response.success || !response.data) {
        throw new Error(formatApiMessage(response));
      }
      setPrecheck(response.data);
      setJob(response.data.job);
    });

  const handleStart = () =>
    guarded("start", async () => {
      const currentJob = await ensureJob();
      const response = await setupConsoleApi.startMigrationJob(currentJob.id);
      if (!response.success || !response.data) {
        throw new Error(formatApiMessage(response));
      }
      setJob(response.data);
      setStep(3);
    });

  const handleCancel = () =>
    guarded("cancel", async () => {
      if (!job) {
        return;
      }
      const response = await setupConsoleApi.cancelMigrationJob(job.id);
      if (!response.success || !response.data) {
        throw new Error(formatApiMessage(response));
      }
      setJob(response.data);
    });

  const handleValidate = () =>
    guarded("validate", async () => {
      if (!job) {
        return;
      }
      const response = await setupConsoleApi.validateMigrationJob(job.id);
      if (!response.success || !response.data) {
        throw new Error(formatApiMessage(response));
      }
      setReport(response.data);
    });

  const handleCutover = () =>
    guarded("cutover", async () => {
      if (!job) {
        return;
      }
      const response = await setupConsoleApi.cutoverMigrationJob(job.id, {
        keepSourceReadonlyForDays: 7,
        confirmBackup: cutoverConfirmed,
        confirmRestartRequired: restartConfirmed
      });
      if (!response.success || !response.data) {
        throw new Error(formatApiMessage(response));
      }
      setJob(response.data);
      Toast.success(t("setupConsoleMigrationCutoverCompleted"));
    });

  const targetColumns: ColumnProps<TenantDataSourceDto>[] = [
    {
      title: t("cozeLibraryCreateName"),
      dataIndex: "name",
      render: (_value, record) => (
        <div>
          <Text strong>{record.name}</Text>
          <div>
            <Text type="tertiary">{formatTenantDataSourceSummary(record)}</Text>
          </div>
        </div>
      )
    },
    { title: t("setupDatabaseDriver"), dataIndex: "dbType", width: 140 },
    { title: t("setupConsoleMigrationTargetHost"), dataIndex: "host", width: 160, render: value => value || "-" },
    { title: t("setupConsoleMigrationTargetDatabase"), dataIndex: "databaseName", width: 160, render: value => value || "-" },
    { title: t("setupConsoleMigrationColumnUpdatedAt"), dataIndex: "createdAt", width: 180, render: value => formatTime(String(value)) }
  ];

  const progressRows = progress?.tables ?? precheck?.tables ?? [];
  const progressColumns: ColumnProps<DataMigrationTableProgressDto>[] = [
    { title: t("setupConsoleMigrationTableName"), dataIndex: "tableName", width: 220 },
    { title: t("setupConsoleMigrationReportEntityColumn"), dataIndex: "entityName", width: 180 },
    { title: t("setupConsoleMigrationReportSourceCountColumn"), dataIndex: "sourceRows", width: 120 },
    { title: t("setupConsoleMigrationTargetBefore"), dataIndex: "targetRowsBefore", width: 130 },
    { title: t("setupConsoleMigrationProgressCopiedRows"), dataIndex: "copiedRows", width: 120 },
    { title: t("setupConsoleMigrationTargetAfter"), dataIndex: "targetRowsAfter", width: 130 },
    { title: t("setupConsoleMigrationProgressCurrentBatch"), dataIndex: "currentBatchNo", width: 110 },
    { title: t("setupConsoleMigrationTotalBatch"), dataIndex: "totalBatchCount", width: 110 },
    { title: t("setupConsoleMigrationColumnState"), dataIndex: "state", width: 120 },
    {
      title: t("setupConsoleMigrationColumnProgress"),
      dataIndex: "progressPercent",
      width: 150,
      render: value => <Progress percent={Number(value ?? 0)} size="small" showInfo />
    },
    { title: t("setupConsoleStepStateFailed"), dataIndex: "errorMessage", width: 220, render: value => value || "-" }
  ];

  const renderStep = () => {
    if (!source) {
      return <Banner type="danger" description={t("setupConsoleMigrationSourceMissing")} />;
    }

    if (step === 0) {
      return (
        <Card>
          <Title heading={6}>{t("setupConsoleMigrationSourceTitle")}</Title>
          <Space vertical align="start">
            <Text>{t("cozeLibraryCreateName")}: {source.name}</Text>
            <Text>{t("cozeLibraryColumnType")}: {source.typeLabel ?? source.resourceType}</Text>
            <Text>{t("setupConsoleMigrationResourceId")}: {source.resourceId}</Text>
            <Text>{t("cozeLibraryColumnUpdatedAt")}: {formatTime(source.updatedAt)}</Text>
            <Banner type="info" description={t("setupConsoleMigrationAiDatabaseSourceHint")} />
          </Space>
        </Card>
      );
    }

    if (step === 1) {
      return (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <Tabs
            type="button"
            activeKey="saved"
            tabList={[
              { itemKey: "saved", tab: t("setupConsoleMigrationSavedDataSource") },
              { itemKey: "new", tab: t("setupConsoleMigrationNewDataSource") }
            ]}
            onChange={key => {
              if (key === "new") {
                setCreateDataSourceOpen(true);
              }
            }}
          />
          <div style={{ display: "flex", justifyContent: "space-between", gap: 12 }}>
            <Input
              prefix={null}
              value={targetKeyword}
              onChange={setTargetKeyword}
              placeholder={t("cozeLibrarySearchPlaceholder")}
              showClear
              style={{ width: 280 }}
            />
            <Button icon={<IconRefresh />} loading={loadingTargets} onClick={() => void loadTargets()}>
              {t("setupConsoleRefresh")}
            </Button>
          </div>
          <Table
            columns={targetColumns}
            dataSource={filteredTargets}
            loading={loadingTargets}
            rowKey="id"
            pagination={false}
            rowSelection={{
              selectedRowKeys: targetId ? [targetId] : [],
              onChange: selected => handleTargetSelect(selected as (string | number)[])
            }}
          />
          <Space>
            <Button disabled={!targetId} onClick={() => void guarded("test-target", async () => {
              if (!targetId) {
                return;
              }
              const result = await testTenantDataSourceById(targetId);
              if (result.success) {
                Toast.success(t("setupTestSuccess"));
              } else {
                throw new Error(result.errorMessage ?? t("loginFailed"));
              }
            })}>
              {t("setupTestConnection")}
            </Button>
            <Button onClick={() => setCreateDataSourceOpen(true)}>{t("setupConsoleMigrationNewDataSource")}</Button>
          </Space>
        </div>
      );
    }

    if (step === 2) {
      return (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <Card>
            <Space vertical align="start">
              <Text strong>{t("setupConsoleMigrationTargetTitle")}: {selectedTarget?.name ?? "-"}</Text>
              <Text type="tertiary">{selectedTarget ? formatTenantDataSourceSummary(selectedTarget) : "-"}</Text>
            </Space>
          </Card>
          <Card>
            <Space vertical align="start" style={{ width: "100%" }}>
              <ConfigField label={t("setupConsoleMigrationScopeLabel")}>
                <Select
                  value={config.scopeMode}
                  onChange={value => updateConfig(current => ({ ...current, scopeMode: String(value) as MigrationConfigState["scopeMode"] }))}
                  optionList={[
                    { value: "all", label: t("setupConsoleMigrationScopeAll") },
                    { value: "module", label: t("setupConsoleMigrationScopeByCategory") },
                    { value: "table", label: t("setupConsoleMigrationScopeByEntity") }
                  ]}
                  style={{ width: 240 }}
                />
              </ConfigField>
              {config.scopeMode === "table" ? (
                <ConfigField label={t("setupConsoleMigrationSelectedTables")}>
                  <Input.TextArea
                    value={config.selectedTablesText}
                    onChange={value => updateConfig(current => ({ ...current, selectedTablesText: value }))}
                    placeholder={t("setupConsoleMigrationSelectedTablesPlaceholder")}
                    autosize={{ minRows: 3, maxRows: 6 }}
                  />
                </ConfigField>
              ) : null}
              <ConfigField label={t("setupConsoleMigrationWriteMode")}>
                <Select
                  value={config.writeMode}
                  onChange={value => updateConfig(current => ({ ...current, writeMode: String(value) as WriteMode }))}
                  optionList={[
                    { value: "InsertOnly", label: "InsertOnly" },
                    { value: "TruncateThenInsert", label: "TruncateThenInsert" },
                    { value: "Upsert", label: "Upsert", disabled: true }
                  ]}
                  style={{ width: 240 }}
                />
              </ConfigField>
              <Text type="tertiary">{t("setupConsoleMigrationUpsertDisabledHint")}</Text>
              <ConfigField label={t("setupConsoleMigrationBatchSize")}>
                <InputNumber
                  value={config.batchSize}
                  min={100}
                  max={100000}
                  step={1000}
                  onChange={value => updateConfig(current => ({ ...current, batchSize: Number(value ?? 10000) }))}
                />
              </ConfigField>
              <Checkbox checked={config.createSchema} onChange={event => updateConfig(current => ({ ...current, createSchema: Boolean(event.target.checked) }))}>
                {t("setupConsoleMigrationCreateSchema")}
              </Checkbox>
              <Checkbox checked={config.migrateSystemTables} onChange={event => updateConfig(current => ({ ...current, migrateSystemTables: Boolean(event.target.checked) }))}>
                {t("setupConsoleMigrationMigrateSystemTables")}
              </Checkbox>
              <Checkbox checked={config.migrateFiles} onChange={event => updateConfig(current => ({ ...current, migrateFiles: Boolean(event.target.checked) }))}>
                {t("setupConsoleMigrationMigrateFiles")}
              </Checkbox>
              <Checkbox checked={config.validateAfterCopy} onChange={event => updateConfig(current => ({ ...current, validateAfterCopy: Boolean(event.target.checked) }))}>
                {t("setupConsoleMigrationValidateAfterCopy")}
              </Checkbox>
              <Button loading={busy === "precheck"} disabled={!selectedTarget} onClick={() => void handlePrecheck()}>
                {t("setupConsoleMigrationPrecheck")}
              </Button>
              {!precheck ? <Banner type="info" description={t("setupConsoleMigrationPrecheckRequired")} /> : null}
            </Space>
          </Card>
          {precheck ? (
            <Card>
              <Space vertical align="start">
                <Text>{t("setupConsoleMigrationTableCount")}: {precheck.tableCount}</Text>
                <Text>{t("setupConsoleMigrationProgressTotalRows")}: {precheck.totalRows}</Text>
                <Text>{t("setupConsoleMigrationEstimatedBatches")}: {precheck.estimatedBatches}</Text>
                {precheck.warnings.length > 0 ? <Banner type="warning" description={precheck.warnings.join("；")} /> : null}
                {precheck.targetNonEmptyTables.length > 0 ? (
                  <Banner type="warning" description={`${t("setupConsoleMigrationTargetNonEmpty")}: ${precheck.targetNonEmptyTables.join(", ")}`} />
                ) : null}
                {precheck.missingTargetTables.length > 0 ? (
                  <Banner type="warning" description={`${t("setupConsoleMigrationMissingTargets")}: ${precheck.missingTargetTables.join(", ")}`} />
                ) : null}
              </Space>
            </Card>
          ) : null}
        </div>
      );
    }

    if (step === 3) {
      const currentProgress = progress?.progressPercent ?? job?.progressPercent ?? 0;
      return (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <Card>
            <Progress percent={Number(currentProgress)} showInfo />
            <div style={{ display: "grid", gridTemplateColumns: "repeat(4, minmax(0, 1fr))", gap: 12, marginTop: 16 }}>
              <Metric label={t("setupConsoleMigrationProgressCompletedEntities")} value={`${progress?.completedEntities ?? job?.completedEntities ?? 0} / ${progress?.totalEntities ?? job?.totalEntities ?? 0}`} />
              <Metric label={t("setupConsoleMigrationProgressCopiedRows")} value={`${progress?.copiedRows ?? job?.copiedRows ?? 0} / ${progress?.totalRows ?? job?.totalRows ?? 0}`} />
              <Metric label={t("setupConsoleMigrationProgressFailedEntities")} value={String(progress?.failedEntities ?? job?.failedEntities ?? 0)} />
              <Metric label={t("setupConsoleMigrationProgressCurrentEntity")} value={progress?.currentTableName ?? progress?.currentEntityName ?? job?.currentTableName ?? "-"} />
              <Metric label={t("setupConsoleMigrationProgressCurrentBatch")} value={String(progress?.currentBatchNo ?? job?.currentBatchNo ?? "-")} />
              <Metric label={t("setupConsoleMigrationColumnState")} value={progress?.state ?? job?.state ?? "-"} />
              <Metric label={t("setupConsoleMigrationStartedAt")} value={formatTime(progress?.startedAt ?? job?.startedAt)} />
              <Metric label={t("setupConsoleMigrationElapsed")} value={`${progress?.elapsedSeconds ?? 0}s`} />
            </div>
            <Space style={{ marginTop: 16 }}>
              <Tooltip content={t("setupConsoleMigrationPauseUnsupported")}>
                <Button disabled>{t("setupConsoleMigrationPause")}</Button>
              </Tooltip>
              <Button type="danger" theme="solid" loading={busy === "cancel"} disabled={!job || isTerminalState(progress?.state ?? job.state)} onClick={() => void handleCancel()}>
                {t("cozeCommonCancel")}
              </Button>
            </Space>
          </Card>
          {!canEnterValidateStep ? (
            <Banner type="info" description={t("setupConsoleMigrationProgressNotReady")} />
          ) : null}
          <Table columns={progressColumns} dataSource={progressRows} rowKey="tableName" pagination={false} scroll={{ y: 360 }} />
        </div>
      );
    }

    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
        <Card>
          <Space>
            <Button loading={busy === "validate"} disabled={!job} onClick={() => void handleValidate()}>
              {t("setupConsoleMigrationValidate")}
            </Button>
            <Checkbox checked={cutoverConfirmed} onChange={event => setCutoverConfirmed(Boolean(event.target.checked))}>
              {t("setupConsoleMigrationConfirmBackup")}
            </Checkbox>
            <Checkbox checked={restartConfirmed} onChange={event => setRestartConfirmed(Boolean(event.target.checked))}>
              {t("setupConsoleMigrationConfirmRestart")}
            </Checkbox>
            <Button
              type="primary"
              theme="solid"
              loading={busy === "cutover"}
              disabled={!job || !report?.overallPassed || !cutoverConfirmed || !restartConfirmed}
              onClick={() => void handleCutover()}
            >
              {t("setupConsoleMigrationCutover")}
            </Button>
          </Space>
          <Banner type="warning" description={t("setupConsoleMigrationCutoverWarning")} style={{ marginTop: 12 }} />
        </Card>
        {report ? (
          <Card>
            <Text strong>
              {report.overallPassed ? t("setupConsoleMigrationReportPassed") : t("setupConsoleMigrationReportFailed")}
            </Text>
            <Table
              columns={[
                { title: t("setupConsoleMigrationTableName"), dataIndex: "tableName" },
                { title: t("setupConsoleMigrationReportSourceCountColumn"), dataIndex: "sourceRowCount" },
                { title: t("setupConsoleMigrationReportTargetCountColumn"), dataIndex: "targetRowCount" },
                { title: t("setupConsoleMigrationReportDiffColumn"), dataIndex: "diff" },
                { title: t("setupConsoleMigrationColumnState"), dataIndex: "state" },
                { title: t("setupConsoleStepStateFailed"), dataIndex: "errorMessage", render: value => value || "-" }
              ]}
              dataSource={report.rowDiff.map(item => ({ ...item, key: item.tableName || item.entityName }))}
              pagination={false}
            />
          </Card>
        ) : null}
      </div>
    );
  };

  const canUseNext =
    (step === 0 && Boolean(source)) ||
    (step === 1 && Boolean(selectedTarget)) ||
    (step === 3 && canEnterValidateStep);
  const showNextButton = step !== 2 && step < 4;
  const handleNext = () => {
    if (step === 3 && !canEnterValidateStep) {
      Toast.warning(t("setupConsoleMigrationProgressNotReady"));
      return;
    }
    setStep(current => Math.min(4, current + 1));
  };
  const handleStartFromConfig = () => {
    if (!precheck) {
      Toast.warning(t("setupConsoleMigrationPrecheckRequired"));
      return;
    }
    void handleStart();
  };

  return (
    <SideSheet
      title={t("setupConsoleMigrationWizardTitle")}
      visible={visible}
      onCancel={onClose}
      width={1080}
      footer={
        <div style={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
          <Button disabled={step <= 0 || busy !== null} onClick={() => setStep(current => Math.max(0, current - 1))}>
            {t("cozeCommonBack")}
          </Button>
          <Space>
            {step === 2 ? (
              <Button
                type="primary"
                theme="solid"
                loading={busy === "start"}
                disabled={!selectedTarget || busy !== null}
                onClick={handleStartFromConfig}
              >
                {t("setupConsoleMigrationStart")}
              </Button>
            ) : null}
            {showNextButton ? (
              <Button
                type="primary"
                theme="solid"
                disabled={!canUseNext || busy !== null}
                onClick={handleNext}
              >
                {t("cozeCommonNext")}
              </Button>
            ) : null}
          </Space>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
        <Steps current={step} size="small">
          <Steps.Step title={t("setupConsoleMigrationStepSource")} />
          <Steps.Step title={t("setupConsoleMigrationStepTarget")} />
          <Steps.Step title={t("setupConsoleMigrationStepConfig")} />
          <Steps.Step title={t("setupConsoleMigrationStepProgress")} />
          <Steps.Step title={t("setupConsoleMigrationStepValidate")} />
        </Steps>
        {renderStep()}
      </div>
      <CreateDataSourceModal
        visible={createDataSourceOpen}
        title={t("setupConsoleMigrationNewDataSource")}
        onClose={() => setCreateDataSourceOpen(false)}
        onCreated={async item => {
          setCreateDataSourceOpen(false);
          resetJobState();
          await loadTargets();
          setTargetId(item.id);
        }}
      />
    </SideSheet>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ padding: 12, borderRadius: 8, background: "var(--semi-color-fill-0)" }}>
      <Text type="tertiary" size="small">{label}</Text>
      <div style={{ marginTop: 4, fontWeight: 600 }}>{value}</div>
    </div>
  );
}

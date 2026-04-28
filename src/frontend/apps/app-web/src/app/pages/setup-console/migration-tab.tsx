import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Button,
  Checkbox,
  Input,
  InputNumber,
  Progress,
  Select,
  Table,
  Typography
} from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useAppI18n } from "../../i18n";
import { useBootstrap } from "../../bootstrap-context";
import type {
  DataMigrationJobDto,
  DataMigrationLogItemDto,
  DataMigrationProgressDto,
  DataMigrationReportDto,
  DbConnectionConfig
} from "../../../services/api-setup-console";
import { setupConsoleApi } from "../../../services/api-setup-console";
import type { AppMessageKey } from "../../messages";
import {
  isMigrationBusy,
  isMigrationDone,
  type DataMigrationMode,
  type DataMigrationState
} from "../../setup-console-state-machine";
import {
  InfoBanner,
  SectionCard,
  StateBadge,
  type StateBadgeVariant
} from "../../_shared";

const { Text } = Typography;

const testMigrationConnection = setupConsoleApi.migrationTestConnection;
const createMigrationJob = setupConsoleApi.createMigrationJob;
const precheckMigrationJob = setupConsoleApi.precheckMigrationJob;
const startMigrationJob = setupConsoleApi.startMigrationJob;
const getMigrationProgress = setupConsoleApi.getMigrationProgress;
const validateMigrationJob = setupConsoleApi.validateMigrationJob;
const cutoverMigrationJob = setupConsoleApi.cutoverMigrationJob;
const retryMigrationJob = setupConsoleApi.retryMigrationJob;
const getMigrationReport = setupConsoleApi.getMigrationReport;
const getMigrationLogs = setupConsoleApi.getMigrationLogs;

interface MigrationTabProps {
  activeMigration: DataMigrationJobDto | null;
  onSnapshotChanged: () => Promise<void>;
}

const MIGRATION_MODE_OPTIONS: ReadonlyArray<{ value: DataMigrationMode; labelKey: AppMessageKey }> = [
  { value: "structure-only", labelKey: "setupConsoleMigrationModeStructureOnly" },
  { value: "structure-plus-data", labelKey: "setupConsoleMigrationModeStructurePlusData" },
  { value: "validate-only", labelKey: "setupConsoleMigrationModeValidateOnly" },
  { value: "incremental-delta", labelKey: "setupConsoleMigrationModeIncrementalDelta" },
  { value: "re-execute", labelKey: "setupConsoleMigrationModeReExecute" }
];

const MIGRATION_STATE_LABEL_KEY: Record<string, AppMessageKey> = {
  created: "setupConsoleMigrationStateCreated",
  pending: "setupConsoleMigrationStatePending",
  prechecking: "setupConsoleMigrationStatePrechecking",
  ready: "setupConsoleMigrationStateReady",
  queued: "setupConsoleMigrationStateQueued",
  running: "setupConsoleMigrationStateRunning",
  cancelling: "setupConsoleMigrationStateCancelling",
  cancelled: "setupConsoleMigrationStateCancelled",
  succeeded: "setupConsoleMigrationStateSucceeded",
  validating: "setupConsoleMigrationStateValidating",
  validation_failed: "setupConsoleMigrationStateValidationFailed",
  validated: "setupConsoleMigrationStateValidated",
  "cutover-ready": "setupConsoleMigrationStateCutoverReady",
  cutover_ready: "setupConsoleMigrationStateCutoverReady",
  "cutover-completed": "setupConsoleMigrationStateCutoverCompleted",
  cutover_completed: "setupConsoleMigrationStateCutoverCompleted",
  cutover_failed: "setupConsoleMigrationStateCutoverFailed",
  "cutover-failed": "setupConsoleMigrationStateCutoverFailed",
  failed: "setupConsoleMigrationStateFailed",
  "rolled-back": "setupConsoleMigrationStateRolledBack"
};

const DRIVER_OPTIONS: ReadonlyArray<{ code: string; dbType: DbConnectionConfig["dbType"] }> = [
  { code: "SQLite", dbType: "SQLite" },
  { code: "MySql", dbType: "MySql" },
  { code: "PostgreSQL", dbType: "PostgreSQL" },
  { code: "SqlServer", dbType: "SqlServer" }
];

const INITIAL_SOURCE: DbConnectionConfig = {
  driverCode: "SQLite",
  dbType: "SQLite",
  mode: "ConnectionString",
  connectionString: ""
};

const INITIAL_TARGET: DbConnectionConfig = {
  driverCode: "MySql",
  dbType: "MySql",
  mode: "ConnectionString",
  connectionString: ""
};

export function MigrationTab({ activeMigration, onSnapshotChanged }: MigrationTabProps) {
  const { t } = useAppI18n();
  const { refreshSetupConsole } = useBootstrap();
  const [source, setSource] = useState<DbConnectionConfig>(INITIAL_SOURCE);
  const [target, setTarget] = useState<DbConnectionConfig>(INITIAL_TARGET);
  const [mode, setMode] = useState<DataMigrationMode>("structure-plus-data");
  const [allowReExecute, setAllowReExecute] = useState(false);
  const [keepSourceReadonlyForDays, setKeepSourceReadonlyForDays] = useState(7);
  const [busy, setBusy] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [sourceTestResult, setSourceTestResult] = useState<string | null>(null);
  const [targetTestResult, setTargetTestResult] = useState<string | null>(null);
  const [progress, setProgress] = useState<DataMigrationProgressDto | null>(null);
  const [report, setReport] = useState<DataMigrationReportDto | null>(null);
  const [logs, setLogs] = useState<DataMigrationLogItemDto[]>([]);
  const [job, setJob] = useState<DataMigrationJobDto | null>(activeMigration);

  const refreshSnapshot = useCallback(async () => {
    await refreshSetupConsole();
    await onSnapshotChanged();
  }, [onSnapshotChanged, refreshSetupConsole]);

  const refreshProgressFor = useCallback(async (jobId: string) => {
    const response = await getMigrationProgress(jobId);
    if (response.success && response.data) {
      setProgress(response.data);
    }
  }, []);

  const refreshLogsFor = useCallback(async (jobId: string) => {
    const response = await getMigrationLogs(jobId, { pageIndex: 1, pageSize: 50 });
    if (response.success && response.data) {
      setLogs(response.data.items);
    }
  }, []);

  // 控制台进入 / 切换 active job 时同步基础 job 信息与日志，
  // 但不自动拉取 progress：在 mock 实现中 progress 调用本身会推进游标，自动 polling 会
  // 把状态从 ready/running 自动推到 validating/cutover-ready，破坏用户的状态机控制。
  // Progress 仅在用户点击 “progress-poll” 按钮时显式获取，与真实后端的“按需 polling”语义一致。
  useEffect(() => {
    if (activeMigration) {
      setJob(activeMigration);
      void refreshLogsFor(activeMigration.id);
    }
  }, [activeMigration, refreshLogsFor]);

  const guarded = useCallback(
    async (label: string, executor: () => Promise<void>) => {
      if (busy) {
        return;
      }
      setBusy(label);
      setErrorMessage(null);
      try {
        await executor();
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : t("setupConsoleStepStateFailed"));
      } finally {
        setBusy(null);
        await refreshSnapshot();
      }
    },
    [busy, refreshSnapshot, t]
  );

  const handleTestSource = useCallback(
    () =>
      guarded("test-source", async () => {
        const response = await testMigrationConnection({ connection: source });
        if (response.success && response.data) {
          setSourceTestResult(response.data.message);
        } else {
          setSourceTestResult(response.message ?? "");
        }
      }),
    [guarded, source]
  );

  const handleTestTarget = useCallback(
    () =>
      guarded("test-target", async () => {
        const response = await testMigrationConnection({ connection: target });
        if (response.success && response.data) {
          setTargetTestResult(response.data.message);
        } else {
          setTargetTestResult(response.message ?? "");
        }
      }),
    [guarded, target]
  );

  const handleCreateJob = useCallback(
    () =>
      guarded("create-job", async () => {
        const response = await createMigrationJob({
          source,
          target,
          mode,
          moduleScope: { categories: ["all"] },
          allowReExecute
        });
        if (response.success && response.data) {
          setJob(response.data);
          setProgress(null);
          setReport(null);
          setLogs([]);
        }
      }),
    [allowReExecute, guarded, mode, source, target]
  );

  const handlePrecheck = useCallback(
    () =>
      guarded("precheck", async () => {
        if (!job) {
          return;
        }
        const response = await precheckMigrationJob(job.id);
        if (response.success && response.data) {
          setJob(response.data.job);
        }
        await refreshLogsFor(job.id);
      }),
    [guarded, job, refreshLogsFor]
  );

  const handleStart = useCallback(
    () =>
      guarded("start", async () => {
        if (!job) {
          return;
        }
        const response = await startMigrationJob(job.id);
        if (response.success && response.data) {
          setJob(response.data);
        }
        await refreshProgressFor(job.id);
      }),
    [guarded, job, refreshProgressFor]
  );

  const handleProgressPoll = useCallback(
    () =>
      guarded("poll-progress", async () => {
        if (!job) {
          return;
        }
        await refreshProgressFor(job.id);
      }),
    [guarded, job, refreshProgressFor]
  );

  const handleValidate = useCallback(
    () =>
      guarded("validate", async () => {
        if (!job) {
          return;
        }
        const response = await validateMigrationJob(job.id);
        if (response.success && response.data) {
          setReport(response.data);
        }
        await refreshProgressFor(job.id);
      }),
    [guarded, job, refreshProgressFor]
  );

  const handleCutover = useCallback(
    () =>
      guarded("cutover", async () => {
        if (!job) {
          return;
        }
        const response = await cutoverMigrationJob(job.id, {
          keepSourceReadonlyForDays,
          confirmBackup: true,
          confirmRestartRequired: true
        });
        if (response.success && response.data) {
          setJob(response.data);
        }
      }),
    [guarded, job, keepSourceReadonlyForDays]
  );

  const handleRetry = useCallback(
    () =>
      guarded("retry", async () => {
        if (!job) {
          return;
        }
        const response = await retryMigrationJob(job.id);
        if (response.success && response.data) {
          setJob(response.data);
        }
      }),
    [guarded, job]
  );

  const handleFetchReport = useCallback(
    () =>
      guarded("fetch-report", async () => {
        if (!job) {
          return;
        }
        const response = await getMigrationReport(job.id);
        if (response.success && response.data) {
          setReport(response.data);
        }
      }),
    [guarded, job]
  );

  const jobBusy = useMemo(() => (job ? isMigrationBusy(job.state as DataMigrationState) : false), [job]);
  const jobDone = useMemo(() => (job ? isMigrationDone(job.state as DataMigrationState) : false), [job]);

  return (
    <div data-testid="setup-console-migration">
      {errorMessage ? (
        <div style={{ marginBottom: 12 }}>
          <InfoBanner
            variant="danger"
            title={t("setupConsoleStepStateFailed")}
            description={errorMessage}
            testId="setup-console-migration-error"
          />
        </div>
      ) : null}

      <PlanSection
        source={source}
        target={target}
        mode={mode}
        allowReExecute={allowReExecute}
        sourceTestResult={sourceTestResult}
        targetTestResult={targetTestResult}
        keepSourceReadonlyForDays={keepSourceReadonlyForDays}
        busyLabel={busy}
        hasJob={Boolean(job)}
        onSourceChange={setSource}
        onTargetChange={setTarget}
        onModeChange={setMode}
        onAllowReExecuteChange={setAllowReExecute}
        onKeepSourceReadonlyForDaysChange={setKeepSourceReadonlyForDays}
        onTestSource={() => void handleTestSource()}
        onTestTarget={() => void handleTestTarget()}
        onCreateJob={() => void handleCreateJob()}
      />

      <ExecuteSection
        job={job}
        busyLabel={busy}
        jobBusy={jobBusy}
        jobDone={jobDone}
        onPrecheck={() => void handlePrecheck()}
        onStart={() => void handleStart()}
        onValidate={() => void handleValidate()}
        onCutover={() => void handleCutover()}
        onRetry={() => void handleRetry()}
      />

      <ProgressSection
        job={job}
        progress={progress}
        busyLabel={busy}
        onPoll={() => void handleProgressPoll()}
      />

      <ReportSection
        report={report}
        logs={logs}
        busyLabel={busy}
        onFetchReport={() => void handleFetchReport()}
      />
    </div>
  );
}

interface PlanSectionProps {
  source: DbConnectionConfig;
  target: DbConnectionConfig;
  mode: DataMigrationMode;
  allowReExecute: boolean;
  sourceTestResult: string | null;
  targetTestResult: string | null;
  keepSourceReadonlyForDays: number;
  busyLabel: string | null;
  hasJob: boolean;
  onSourceChange: (next: DbConnectionConfig) => void;
  onTargetChange: (next: DbConnectionConfig) => void;
  onModeChange: (next: DataMigrationMode) => void;
  onAllowReExecuteChange: (next: boolean) => void;
  onKeepSourceReadonlyForDaysChange: (next: number) => void;
  onTestSource: () => void;
  onTestTarget: () => void;
  onCreateJob: () => void;
}

function FieldStack({ label, children }: { label: React.ReactNode; children: React.ReactNode }) {
  return (
    <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <Text strong>{label}</Text>
      {children}
    </label>
  );
}

function PlanSection(props: PlanSectionProps) {
  const { t } = useAppI18n();
  const submitBusy = props.busyLabel !== null;
  return (
    <div data-testid="setup-console-migration-plan">
      <SectionCard title={t("setupConsoleMigrationCreate")}>
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <ConnectionEditor
            testIdPrefix="setup-console-migration-source"
            titleKey="setupConsoleMigrationSourceTitle"
            value={props.source}
            onChange={props.onSourceChange}
            onTest={props.onTestSource}
            testResult={props.sourceTestResult}
            busy={submitBusy}
          />
          <ConnectionEditor
            testIdPrefix="setup-console-migration-target"
            titleKey="setupConsoleMigrationTargetTitle"
            value={props.target}
            onChange={props.onTargetChange}
            onTest={props.onTestTarget}
            testResult={props.targetTestResult}
            busy={submitBusy}
          />

          <FieldStack label={t("setupConsoleMigrationModeLabel")}>
            <Select
              data-testid="setup-console-migration-mode"
              value={props.mode}
              onChange={(value) => props.onModeChange(String(value ?? "structure-plus-data") as DataMigrationMode)}
              disabled={submitBusy}
              optionList={MIGRATION_MODE_OPTIONS.map((option) => ({
                label: t(option.labelKey),
                value: option.value
              }))}
              style={{ width: "100%" }}
            />
          </FieldStack>

          <label style={{ display: "inline-flex", alignItems: "flex-start", gap: 8 }}>
            <Checkbox
              data-testid="setup-console-migration-allow-reexecute"
              checked={props.allowReExecute}
              onChange={(event) => props.onAllowReExecuteChange(Boolean(event.target.checked))}
              disabled={submitBusy}
            />
            <div style={{ display: "flex", flexDirection: "column" }}>
              <Text>{t("setupConsoleMigrationAllowReExecuteLabel")}</Text>
              <Text type="tertiary" style={{ fontSize: 12 }}>
                {t("setupConsoleMigrationAllowReExecuteHint")}
              </Text>
            </div>
          </label>

          <FieldStack label={t("setupConsoleMigrationKeepSourceLabel")}>
            <InputNumber
              data-testid="setup-console-migration-keep-source-days"
              value={props.keepSourceReadonlyForDays}
              min={0}
              max={90}
              onChange={(value) => {
                const numericValue =
                  typeof value === "number" ? value : Number.parseInt(String(value ?? "0"), 10) || 0;
                props.onKeepSourceReadonlyForDaysChange(numericValue);
              }}
              disabled={submitBusy}
            />
          </FieldStack>

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              type="primary"
              theme="solid"
              data-testid="setup-console-migration-create"
              disabled={submitBusy || props.hasJob}
              onClick={props.onCreateJob}
            >
              {t("setupConsoleMigrationCreate")}
            </Button>
          </div>
        </div>
      </SectionCard>
    </div>
  );
}

interface ConnectionEditorProps {
  testIdPrefix: string;
  titleKey: AppMessageKey;
  value: DbConnectionConfig;
  busy: boolean;
  testResult: string | null;
  onChange: (next: DbConnectionConfig) => void;
  onTest: () => void;
}

function ConnectionEditor(props: ConnectionEditorProps) {
  const { t } = useAppI18n();

  const handleDriverChange = (value: string | number | unknown[] | Record<string, unknown> | undefined) => {
    const code = String(value ?? "");
    const found = DRIVER_OPTIONS.find((item) => item.code === code) ?? DRIVER_OPTIONS[0];
    props.onChange({
      ...props.value,
      driverCode: code,
      dbType: found.dbType
    });
  };

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 12,
        padding: 12,
        borderRadius: 8,
        border: "1px solid var(--semi-color-border)"
      }}
    >
      <Text strong>{t(props.titleKey)}</Text>
      <FieldStack label={t("setupConsoleMigrationDriverLabel")}>
        <Select
          data-testid={`${props.testIdPrefix}-driver`}
          value={props.value.driverCode}
          onChange={handleDriverChange}
          disabled={props.busy}
          optionList={DRIVER_OPTIONS.map((option) => ({ label: option.code, value: option.code }))}
          style={{ width: "100%" }}
        />
      </FieldStack>
      <FieldStack label={t("setupConsoleMigrationConnectionStringLabel")}>
        <Input
          data-testid={`${props.testIdPrefix}-connection`}
          value={props.value.connectionString ?? ""}
          onChange={(value) => props.onChange({ ...props.value, connectionString: value })}
          disabled={props.busy}
        />
      </FieldStack>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 8 }}>
        <Button
          type="tertiary"
          theme="light"
          data-testid={`${props.testIdPrefix}-test`}
          onClick={props.onTest}
          disabled={props.busy}
        >
          {t("setupTestConnection")}
        </Button>
      </div>
      {props.testResult ? (
        <StateBadge variant="info" testId={`${props.testIdPrefix}-test-result`}>
          {props.testResult}
        </StateBadge>
      ) : null}
    </div>
  );
}

interface ExecuteSectionProps {
  job: DataMigrationJobDto | null;
  busyLabel: string | null;
  jobBusy: boolean;
  jobDone: boolean;
  onPrecheck: () => void;
  onStart: () => void;
  onValidate: () => void;
  onCutover: () => void;
  onRetry: () => void;
}

function executeStateVariant(jobBusy: boolean, jobDone: boolean): StateBadgeVariant {
  if (jobDone) return "success";
  if (jobBusy) return "info";
  return "neutral";
}

function ExecuteSection({
  job,
  busyLabel,
  jobBusy,
  jobDone,
  onPrecheck,
  onStart,
  onValidate,
  onCutover,
  onRetry
}: ExecuteSectionProps) {
  const { t } = useAppI18n();
  const submitBusy = busyLabel !== null;

  if (!job) {
    return (
      <div data-testid="setup-console-migration-execute">
        <SectionCard title={t("setupConsoleMigrationStart")}>
          <Text type="tertiary">{t("setupConsoleMigrationEmpty")}</Text>
        </SectionCard>
      </div>
    );
  }

  return (
    <div data-testid="setup-console-migration-execute">
      <SectionCard
        title={t("setupConsoleMigrationStart")}
        subtitle={
          <span>
            {t("setupConsoleMigrationColumnId")}: <code>{job.id}</code>
          </span>
        }
        actions={
          <StateBadge
            variant={executeStateVariant(jobBusy, jobDone)}
            testId="setup-console-migration-execute-state"
          >
            {t(MIGRATION_STATE_LABEL_KEY[job.state] ?? "setupConsoleMigrationStatePending")}
          </StateBadge>
        }
      >
        <div style={{ display: "flex", flexWrap: "wrap", justifyContent: "flex-end", gap: 8 }}>
          <Button
            type="tertiary"
            theme="light"
            data-testid="setup-console-migration-precheck"
            disabled={submitBusy || jobDone}
            onClick={onPrecheck}
          >
            {t("setupConsoleMigrationPrecheck")}
          </Button>
          <Button
            type="primary"
            theme="solid"
            data-testid="setup-console-migration-start"
            disabled={submitBusy || jobDone}
            onClick={onStart}
          >
            {t("setupConsoleMigrationStart")}
          </Button>
          <Button
            type="tertiary"
            theme="light"
            data-testid="setup-console-migration-validate"
            disabled={submitBusy || jobDone}
            onClick={onValidate}
          >
            {t("setupConsoleMigrationValidate")}
          </Button>
          <Button
            type="primary"
            theme="solid"
            data-testid="setup-console-migration-cutover"
            disabled={submitBusy || jobDone}
            onClick={onCutover}
          >
            {t("setupConsoleMigrationCutover")}
          </Button>
          <Button
            type="tertiary"
            theme="light"
            data-testid="setup-console-migration-retry"
            disabled={submitBusy}
            onClick={onRetry}
          >
            {t("setupConsoleMigrationRetry")}
          </Button>
        </div>
      </SectionCard>
    </div>
  );
}

interface ProgressSectionProps {
  job: DataMigrationJobDto | null;
  progress: DataMigrationProgressDto | null;
  busyLabel: string | null;
  onPoll: () => void;
}

function ProgressSection({ job, progress, busyLabel, onPoll }: ProgressSectionProps) {
  const { t } = useAppI18n();
  const submitBusy = busyLabel !== null;

  if (!job) {
    return null;
  }

  const percent = progress?.progressPercent ?? job.progressPercent;
  return (
    <div data-testid="setup-console-migration-progress">
      <SectionCard
        title={t("setupConsoleMigrationColumnProgress")}
        subtitle={`${t("setupConsoleMigrationProgressTotalEntities")}: ${
          progress?.totalEntities ?? job.totalEntities
        }, ${t("setupConsoleMigrationProgressCompletedEntities")}: ${
          progress?.completedEntities ?? job.completedEntities
        }, ${t("setupConsoleMigrationProgressFailedEntities")}: ${
          progress?.failedEntities ?? job.failedEntities
        }`}
        actions={
          <Button
            type="tertiary"
            theme="light"
            data-testid="setup-console-migration-progress-poll"
            disabled={submitBusy}
            onClick={onPoll}
          >
            {t("setupConsoleRefresh")}
          </Button>
        }
      >
        <div data-testid="setup-console-migration-progress-bar" aria-valuenow={percent}>
          <Progress percent={percent} stroke="var(--semi-color-primary)" showInfo />
        </div>
        <Text type="tertiary" style={{ display: "block", marginTop: 12 }}>
          {t("setupConsoleMigrationProgressCurrentEntity")}:{" "}
          <code>{progress?.currentEntityName ?? job.currentEntityName ?? "-"}</code>
          {" / "}
          {t("setupConsoleMigrationProgressCurrentBatch")}:{" "}
          <code>{progress?.currentBatchNo ?? job.currentBatchNo ?? "-"}</code>
          {" / "}
          {t("setupConsoleMigrationProgressCopiedRows")}: {progress?.copiedRows ?? job.copiedRows}
        </Text>
      </SectionCard>
    </div>
  );
}

interface ReportSectionProps {
  report: DataMigrationReportDto | null;
  logs: DataMigrationLogItemDto[];
  busyLabel: string | null;
  onFetchReport: () => void;
}

function ReportSection({ report, logs, busyLabel, onFetchReport }: ReportSectionProps) {
  const { t } = useAppI18n();
  const submitBusy = busyLabel !== null;

  const reportColumns: ColumnProps<DataMigrationReportDto["rowDiff"][number]>[] = [
    { title: t("setupConsoleMigrationReportEntityColumn"), dataIndex: "entityName" },
    { title: t("setupConsoleMigrationReportSourceCountColumn"), dataIndex: "sourceRowCount" },
    { title: t("setupConsoleMigrationReportTargetCountColumn"), dataIndex: "targetRowCount" },
    { title: t("setupConsoleMigrationReportDiffColumn"), dataIndex: "diff" }
  ];

  const logColumns: ColumnProps<DataMigrationLogItemDto>[] = [
    { title: t("setupConsoleLogColumnLevel"), dataIndex: "level" },
    {
      title: t("setupConsoleLogColumnEntity"),
      dataIndex: "entityName",
      render: (value) => (typeof value === "string" && value ? value : "-")
    },
    { title: t("setupConsoleLogColumnMessage"), dataIndex: "message" },
    { title: t("setupConsoleLogColumnOccurredAt"), dataIndex: "occurredAt" }
  ];

  return (
    <div data-testid="setup-console-migration-report">
      <SectionCard
        title={t("setupConsoleMigrationViewReport")}
        subtitle={t("setupConsoleMigrationViewLogs")}
        actions={
          <Button
            type="tertiary"
            theme="light"
            data-testid="setup-console-migration-report-fetch"
            disabled={submitBusy}
            onClick={onFetchReport}
          >
            {t("setupConsoleMigrationViewReport")}
          </Button>
        }
      >
        {report ? (
          <div data-testid="setup-console-migration-report-summary">
            <Text type="tertiary" style={{ display: "block", marginBottom: 8 }}>
              {report.overallPassed
                ? t("setupConsoleMigrationReportPassed")
                : t("setupConsoleMigrationReportFailed")}
              : {report.passedEntities} / {report.totalEntities}
            </Text>
            <Table
              columns={reportColumns}
              dataSource={report.rowDiff.map((item) => ({ ...item, key: item.entityName }))}
              pagination={false}
              size="small"
            />
          </div>
        ) : (
          <Text type="tertiary" data-testid="setup-console-migration-report-empty">
            {t("setupConsoleLogEmpty")}
          </Text>
        )}

        <Text strong style={{ display: "block", marginTop: 16, marginBottom: 8 }}>
          {t("setupConsoleMigrationViewLogs")}
        </Text>
        {logs.length === 0 ? (
          <Text type="tertiary">{t("setupConsoleLogEmpty")}</Text>
        ) : (
          <div data-testid="setup-console-migration-logs">
            <Table
              columns={logColumns}
              dataSource={logs.map((item) => ({ ...item, key: item.id }))}
              pagination={false}
              size="small"
            />
          </div>
        )}
      </SectionCard>
    </div>
  );
}

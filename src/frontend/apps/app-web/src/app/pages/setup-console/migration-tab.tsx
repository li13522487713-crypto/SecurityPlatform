import { useCallback, useEffect, useMemo, useState, type ChangeEvent } from "react";
import { useAppI18n } from "../../i18n";
import { useBootstrap } from "../../bootstrap-context";
import {
  createMigrationJob,
  cutoverMigrationJob,
  getMigrationLogs,
  getMigrationProgress,
  getMigrationReport,
  precheckMigrationJob,
  retryMigrationJob,
  rollbackMigrationJob,
  startMigrationJob,
  testMigrationConnection,
  validateMigrationJob
} from "../../../services/mock";
import type {
  DataMigrationJobDto,
  DataMigrationLogItemDto,
  DataMigrationProgressDto,
  DataMigrationReportDto,
  DbConnectionConfig
} from "../../../services/api-setup-console";
import type { AppMessageKey } from "../../messages";
import {
  isMigrationBusy,
  isMigrationDone,
  type DataMigrationMode,
  type DataMigrationState
} from "../../setup-console-state-machine";

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

const MIGRATION_STATE_LABEL_KEY: Record<DataMigrationState, AppMessageKey> = {
  pending: "setupConsoleMigrationStatePending",
  prechecking: "setupConsoleMigrationStatePrechecking",
  ready: "setupConsoleMigrationStateReady",
  running: "setupConsoleMigrationStateRunning",
  validating: "setupConsoleMigrationStateValidating",
  "cutover-ready": "setupConsoleMigrationStateCutoverReady",
  "cutover-completed": "setupConsoleMigrationStateCutoverCompleted",
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
  mode: "raw",
  connectionString: "Data Source=atlas.db"
};

const INITIAL_TARGET: DbConnectionConfig = {
  driverCode: "MySql",
  dbType: "MySql",
  mode: "raw",
  connectionString: "Server=localhost;Port=3306;Database=atlas;Uid=root;Pwd=password;"
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

  // 控制台进入 / 切换 active job 时拉取最新 progress 与 logs
  useEffect(() => {
    if (activeMigration) {
      setJob(activeMigration);
      void refreshProgressFor(activeMigration.id);
      void refreshLogsFor(activeMigration.id);
    }
  }, [activeMigration, refreshLogsFor, refreshProgressFor]);

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
          setJob(response.data);
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
        const response = await cutoverMigrationJob(job.id, { keepSourceReadonlyForDays });
        if (response.success && response.data) {
          setJob(response.data);
        }
      }),
    [guarded, job, keepSourceReadonlyForDays]
  );

  const handleRollback = useCallback(
    () =>
      guarded("rollback", async () => {
        if (!job) {
          return;
        }
        const response = await rollbackMigrationJob(job.id);
        if (response.success && response.data) {
          setJob(response.data);
        }
      }),
    [guarded, job]
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

  const jobBusy = useMemo(() => (job ? isMigrationBusy(job.state) : false), [job]);
  const jobDone = useMemo(() => (job ? isMigrationDone(job.state) : false), [job]);

  return (
    <div data-testid="setup-console-migration">
      {errorMessage ? (
        <div className="atlas-warning-banner" data-testid="setup-console-migration-error">
          <strong>{t("setupConsoleStepStateFailed")}</strong>
          <p>{errorMessage}</p>
        </div>
      ) : null}

      {/* ---- 计划 / Plan ---- */}
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

      {/* ---- 执行 / Execute ---- */}
      <ExecuteSection
        job={job}
        busyLabel={busy}
        jobBusy={jobBusy}
        jobDone={jobDone}
        onPrecheck={() => void handlePrecheck()}
        onStart={() => void handleStart()}
        onValidate={() => void handleValidate()}
        onCutover={() => void handleCutover()}
        onRollback={() => void handleRollback()}
        onRetry={() => void handleRetry()}
      />

      {/* ---- 进度 / Progress ---- */}
      <ProgressSection
        job={job}
        progress={progress}
        busyLabel={busy}
        onPoll={() => void handleProgressPoll()}
      />

      {/* ---- 报告 / Report + Logs ---- */}
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

function PlanSection(props: PlanSectionProps) {
  const { t } = useAppI18n();
  const submitBusy = props.busyLabel !== null;
  return (
    <section className="atlas-setup-panel" data-testid="setup-console-migration-plan">
      <div className="atlas-section-title">{t("setupConsoleMigrationCreate")}</div>
      <div className="atlas-form-grid">
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

        <label className="atlas-form-field atlas-form-field--full">
          <span className="atlas-form-field__label">{t("setupConsoleMigrationModeLabel")}</span>
          <select
            className="atlas-input"
            data-testid="setup-console-migration-mode"
            value={props.mode}
            onChange={(event) => props.onModeChange(event.target.value as DataMigrationMode)}
            disabled={submitBusy}
          >
            {MIGRATION_MODE_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {t(option.labelKey)}
              </option>
            ))}
          </select>
        </label>

        <label className="atlas-form-field" style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
          <input
            type="checkbox"
            data-testid="setup-console-migration-allow-reexecute"
            checked={props.allowReExecute}
            onChange={(event) => props.onAllowReExecuteChange(event.target.checked)}
            disabled={submitBusy}
          />
          <div>
            <div>{t("setupConsoleMigrationAllowReExecuteLabel")}</div>
            <div className="atlas-field-hint">{t("setupConsoleMigrationAllowReExecuteHint")}</div>
          </div>
        </label>

        <label className="atlas-form-field">
          <span className="atlas-form-field__label">{t("setupConsoleMigrationKeepSourceLabel")}</span>
          <input
            className="atlas-input"
            type="number"
            inputMode="numeric"
            data-testid="setup-console-migration-keep-source-days"
            value={props.keepSourceReadonlyForDays}
            min={0}
            max={90}
            onChange={(event) =>
              props.onKeepSourceReadonlyForDaysChange(
                Number.parseInt(event.target.value || "0", 10) || 0
              )
            }
            disabled={submitBusy}
          />
        </label>
      </div>
      <div className="atlas-setup-actions">
        <span />
        <button
          type="button"
          className="atlas-button atlas-button--primary"
          data-testid="setup-console-migration-create"
          disabled={submitBusy || props.hasJob}
          onClick={props.onCreateJob}
        >
          {t("setupConsoleMigrationCreate")}
        </button>
      </div>
    </section>
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
  const handleDriverChange = (event: ChangeEvent<HTMLSelectElement>) => {
    const code = event.target.value;
    const found = DRIVER_OPTIONS.find((item) => item.code === code) ?? DRIVER_OPTIONS[0];
    props.onChange({
      ...props.value,
      driverCode: code,
      dbType: found.dbType
    });
  };

  const handleConnectionStringChange = (event: ChangeEvent<HTMLInputElement>) => {
    props.onChange({ ...props.value, connectionString: event.target.value });
  };

  return (
    <div className="atlas-form-field atlas-form-field--full">
      <span className="atlas-section-title" style={{ fontSize: 14 }}>
        {t(props.titleKey)}
      </span>
      <label className="atlas-form-field">
        <span className="atlas-form-field__label">{t("setupConsoleMigrationDriverLabel")}</span>
        <select
          className="atlas-input"
          data-testid={`${props.testIdPrefix}-driver`}
          value={props.value.driverCode}
          onChange={handleDriverChange}
          disabled={props.busy}
        >
          {DRIVER_OPTIONS.map((option) => (
            <option key={option.code} value={option.code}>
              {option.code}
            </option>
          ))}
        </select>
      </label>
      <label className="atlas-form-field">
        <span className="atlas-form-field__label">{t("setupConsoleMigrationConnectionStringLabel")}</span>
        <input
          className="atlas-input"
          data-testid={`${props.testIdPrefix}-connection`}
          value={props.value.connectionString ?? ""}
          onChange={handleConnectionStringChange}
          disabled={props.busy}
        />
      </label>
      <div className="atlas-setup-actions">
        <span />
        <button
          type="button"
          className="atlas-button atlas-button--secondary"
          data-testid={`${props.testIdPrefix}-test`}
          onClick={props.onTest}
          disabled={props.busy}
        >
          {t("setupTestConnection")}
        </button>
      </div>
      {props.testResult ? (
        <span className="atlas-pill is-info" data-testid={`${props.testIdPrefix}-test-result`}>
          {props.testResult}
        </span>
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
  onRollback: () => void;
  onRetry: () => void;
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
  onRollback,
  onRetry
}: ExecuteSectionProps) {
  const { t } = useAppI18n();
  const submitBusy = busyLabel !== null;

  if (!job) {
    return (
      <section className="atlas-setup-panel" data-testid="setup-console-migration-execute">
        <div className="atlas-section-title">{t("setupConsoleMigrationStart")}</div>
        <p className="atlas-field-hint">{t("setupConsoleMigrationEmpty")}</p>
      </section>
    );
  }

  return (
    <section className="atlas-setup-panel" data-testid="setup-console-migration-execute">
      <div className="atlas-org-section__header">
        <div>
          <div className="atlas-section-title">{t("setupConsoleMigrationStart")}</div>
          <div className="atlas-field-hint">
            {t("setupConsoleMigrationColumnId")}: <code>{job.id}</code>
          </div>
        </div>
        <span
          className={`atlas-pill ${jobDone ? "is-success" : jobBusy ? "is-info" : ""}`.trim()}
          data-testid="setup-console-migration-execute-state"
        >
          {t(MIGRATION_STATE_LABEL_KEY[job.state])}
        </span>
      </div>
      <div className="atlas-setup-actions">
        <span />
        <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
          <button
            type="button"
            className="atlas-button atlas-button--secondary"
            data-testid="setup-console-migration-precheck"
            disabled={submitBusy || jobDone}
            onClick={onPrecheck}
          >
            {t("setupConsoleMigrationPrecheck")}
          </button>
          <button
            type="button"
            className="atlas-button atlas-button--primary"
            data-testid="setup-console-migration-start"
            disabled={submitBusy || jobDone}
            onClick={onStart}
          >
            {t("setupConsoleMigrationStart")}
          </button>
          <button
            type="button"
            className="atlas-button atlas-button--secondary"
            data-testid="setup-console-migration-validate"
            disabled={submitBusy || jobDone}
            onClick={onValidate}
          >
            {t("setupConsoleMigrationValidate")}
          </button>
          <button
            type="button"
            className="atlas-button atlas-button--primary"
            data-testid="setup-console-migration-cutover"
            disabled={submitBusy || jobDone}
            onClick={onCutover}
          >
            {t("setupConsoleMigrationCutover")}
          </button>
          <button
            type="button"
            className="atlas-button atlas-button--danger"
            data-testid="setup-console-migration-rollback"
            disabled={submitBusy || jobDone}
            onClick={onRollback}
          >
            {t("setupConsoleMigrationRollback")}
          </button>
          <button
            type="button"
            className="atlas-button atlas-button--secondary"
            data-testid="setup-console-migration-retry"
            disabled={submitBusy}
            onClick={onRetry}
          >
            {t("setupConsoleMigrationRetry")}
          </button>
        </div>
      </div>
    </section>
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
    <section className="atlas-setup-panel" data-testid="setup-console-migration-progress">
      <div className="atlas-org-section__header">
        <div>
          <div className="atlas-section-title">{t("setupConsoleMigrationColumnProgress")}</div>
          <div className="atlas-field-hint">
            {t("setupConsoleMigrationProgressTotalEntities")}: {progress?.totalEntities ?? job.totalEntities},
            {" "}
            {t("setupConsoleMigrationProgressCompletedEntities")}:{" "}
            {progress?.completedEntities ?? job.completedEntities},
            {" "}
            {t("setupConsoleMigrationProgressFailedEntities")}: {progress?.failedEntities ?? job.failedEntities}
          </div>
        </div>
        <button
          type="button"
          className="atlas-button atlas-button--secondary"
          data-testid="setup-console-migration-progress-poll"
          disabled={submitBusy}
          onClick={onPoll}
        >
          {t("setupConsoleRefresh")}
        </button>
      </div>
      <div className="atlas-progress" data-testid="setup-console-migration-progress-bar" aria-valuenow={percent}>
        <div className="atlas-progress__fill" style={{ width: `${percent}%` }} />
      </div>
      <p className="atlas-field-hint">
        {t("setupConsoleMigrationProgressCurrentEntity")}:{" "}
        <code>{progress?.currentEntityName ?? job.currentEntityName ?? "-"}</code>
        {" / "}
        {t("setupConsoleMigrationProgressCurrentBatch")}:{" "}
        <code>{progress?.currentBatchNo ?? job.currentBatchNo ?? "-"}</code>
        {" / "}
        {t("setupConsoleMigrationProgressCopiedRows")}: {progress?.copiedRows ?? job.copiedRows}
      </p>
    </section>
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
  return (
    <section className="atlas-setup-panel" data-testid="setup-console-migration-report">
      <div className="atlas-org-section__header">
        <div>
          <div className="atlas-section-title">{t("setupConsoleMigrationViewReport")}</div>
          <div className="atlas-field-hint">{t("setupConsoleMigrationViewLogs")}</div>
        </div>
        <button
          type="button"
          className="atlas-button atlas-button--secondary"
          data-testid="setup-console-migration-report-fetch"
          disabled={submitBusy}
          onClick={onFetchReport}
        >
          {t("setupConsoleMigrationViewReport")}
        </button>
      </div>
      {report ? (
        <div data-testid="setup-console-migration-report-summary">
          <p className="atlas-field-hint">
            {report.overallPassed
              ? t("setupConsoleMigrationReportPassed")
              : t("setupConsoleMigrationReportFailed")}
            : {report.passedEntities} / {report.totalEntities}
          </p>
          <table className="atlas-table">
            <thead>
              <tr>
                <th>{t("setupConsoleMigrationReportEntityColumn")}</th>
                <th>{t("setupConsoleMigrationReportSourceCountColumn")}</th>
                <th>{t("setupConsoleMigrationReportTargetCountColumn")}</th>
                <th>{t("setupConsoleMigrationReportDiffColumn")}</th>
              </tr>
            </thead>
            <tbody>
              {report.rowDiff.map((item) => (
                <tr key={item.entityName}>
                  <td>{item.entityName}</td>
                  <td>{item.sourceRowCount}</td>
                  <td>{item.targetRowCount}</td>
                  <td>{item.diff}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="atlas-field-hint" data-testid="setup-console-migration-report-empty">
          {t("setupConsoleLogEmpty")}
        </p>
      )}

      <div className="atlas-section-title" style={{ marginTop: 12, fontSize: 14 }}>
        {t("setupConsoleMigrationViewLogs")}
      </div>
      {logs.length === 0 ? (
        <p className="atlas-field-hint">{t("setupConsoleLogEmpty")}</p>
      ) : (
        <table className="atlas-table" data-testid="setup-console-migration-logs">
          <thead>
            <tr>
              <th>{t("setupConsoleLogColumnLevel")}</th>
              <th>{t("setupConsoleLogColumnEntity")}</th>
              <th>{t("setupConsoleLogColumnMessage")}</th>
              <th>{t("setupConsoleLogColumnOccurredAt")}</th>
            </tr>
          </thead>
          <tbody>
            {logs.map((log) => (
              <tr key={log.id}>
                <td>{log.level}</td>
                <td>{log.entityName ?? "-"}</td>
                <td>{log.message}</td>
                <td>{log.occurredAt}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

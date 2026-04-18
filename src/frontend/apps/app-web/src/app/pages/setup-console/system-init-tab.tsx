import { useCallback, useMemo, useState } from "react";
import { Checkbox, Input, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../../i18n";
import { useBootstrap } from "../../bootstrap-context";
import type { AppMessageKey } from "../../messages";
import {
  bootstrapAdminUser,
  bootstrapDefaultWorkspace,
  completeSystemInit,
  initializeSchema,
  precheckSystem,
  retrySystemStep,
  seedSystem
} from "../../../services/mock";
import type {
  SetupStepRecordDto,
  SystemSetupStateDto
} from "../../../services/api-setup-console";
import {
  isSystemInitDone,
  type SetupConsoleStep,
  type SystemSetupState
} from "../../setup-console-state-machine";
import { InfoBanner, ResultCard, SectionCard } from "../../_shared";
import { StepCard } from "./components/step-card";
import { RecoveryKeyDisplay } from "./components/recovery-key-display";

const { Text, Title } = Typography;

interface SystemInitTabProps {
  system: SystemSetupStateDto | null;
  onSnapshotChanged: () => Promise<void>;
}

interface StepMeta {
  step: SetupConsoleStep;
  titleKey: AppMessageKey;
}

const STEP_ORDER: ReadonlyArray<StepMeta> = [
  { step: "precheck", titleKey: "setupConsoleStepPrecheck" },
  { step: "schema", titleKey: "setupConsoleStepSchema" },
  { step: "seed", titleKey: "setupConsoleStepSeed" },
  { step: "bootstrap-user", titleKey: "setupConsoleStepBootstrapUser" },
  { step: "default-workspace", titleKey: "setupConsoleStepDefaultWorkspace" },
  { step: "complete", titleKey: "setupConsoleStepComplete" }
];

const DEFAULT_TENANT_ID = "00000000-0000-0000-0000-000000000001";

interface AdminFormState {
  username: string;
  password: string;
  tenantId: string;
  generateRecoveryKey: boolean;
  optionalRoleCodes: string[];
}

interface DefaultWorkspaceFormState {
  workspaceName: string;
  ownerUsername: string;
  applyDefaultPublishChannels: boolean;
  applyDefaultModelStub: boolean;
}

const INITIAL_ADMIN_FORM: AdminFormState = {
  username: "admin",
  password: "P@ssw0rd!",
  tenantId: DEFAULT_TENANT_ID,
  generateRecoveryKey: true,
  optionalRoleCodes: []
};

const INITIAL_WORKSPACE_FORM: DefaultWorkspaceFormState = {
  workspaceName: "Default workspace",
  ownerUsername: "admin",
  applyDefaultPublishChannels: true,
  applyDefaultModelStub: true
};

const OPTIONAL_ROLE_CODES = ["SecurityAdmin", "AuditAdmin", "AssetAdmin", "ApprovalAdmin"] as const;

function indexOfState(state: SystemSetupState): number {
  // 仅作 fallback 起点：在没有任何 step 记录时，按系统状态机最近一次显式推进的状态推断当前步骤。
  switch (state) {
    case "not_started":
      return 0;
    case "precheck_passed":
      return 1;
    case "schema_initializing":
    case "schema_initialized":
      return 2;
    case "seed_initializing":
      return 2;
    case "seed_initialized":
      return 3;
    case "completed":
      return STEP_ORDER.length;
    default:
      return 0;
  }
}

/**
 * 计算当前 highlight 的 step index：
 * - 系统状态 `seed_initialized` 之后，bootstrap-user / default-workspace 不会再推动
 *   `SystemSetupState`（两者都映射到 `seed_initialized -> seed_initialized`）。
 *   仅靠 `indexOfState` 会让游标永远卡在 `seed_initialized` 对应的 bootstrap-user，
 *   导致 default-workspace / complete 永远 locked。
 * - 这里改用 step records 的"已 succeeded 的最后一步索引 + 1"作为真理来源，
 *   并以 `indexOfState` 作为状态机层面的"不能比这个更靠后"的安全上限。
 */
function deriveCurrentStepIndex(system: SystemSetupStateDto): number {
  const stateUpperBound = indexOfState(system.state);
  if (stateUpperBound >= STEP_ORDER.length) {
    return STEP_ORDER.length;
  }

  let succeededTip = 0;
  for (let i = 0; i < STEP_ORDER.length; i += 1) {
    const meta = STEP_ORDER[i];
    const record = findRecord(system.steps, meta.step);
    if (record?.state === "succeeded") {
      succeededTip = i + 1;
    } else {
      break;
    }
  }

  return Math.max(succeededTip, stateUpperBound);
}

function findRecord(steps: SetupStepRecordDto[] | undefined, step: SetupConsoleStep): SetupStepRecordDto | null {
  if (!steps) {
    return null;
  }
  const found = steps.find((item) => item.step === step);
  if (!found) {
    return null;
  }
  // running 默认状态过滤：还没真正跑过的步骤不视为有 record
  if (found.attemptCount === 0 && found.state === "running") {
    return null;
  }
  return found;
}

export function SystemInitTab({ system, onSnapshotChanged }: SystemInitTabProps) {
  const { t } = useAppI18n();
  const { refreshSetupConsole } = useBootstrap();
  const [busyStep, setBusyStep] = useState<SetupConsoleStep | null>(null);
  const [adminForm, setAdminForm] = useState<AdminFormState>(INITIAL_ADMIN_FORM);
  const [workspaceForm, setWorkspaceForm] = useState<DefaultWorkspaceFormState>(INITIAL_WORKSPACE_FORM);
  const [recoveryKey, setRecoveryKey] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentStepIndex = useMemo(() => (system ? deriveCurrentStepIndex(system) : 0), [system]);
  const isDone = system ? isSystemInitDone(system.state) : false;

  const updateAdminField = useCallback(
    <K extends keyof AdminFormState>(field: K, value: AdminFormState[K]) => {
      setAdminForm((previous) => ({ ...previous, [field]: value }));
    },
    []
  );

  const updateWorkspaceField = useCallback(
    <K extends keyof DefaultWorkspaceFormState>(field: K, value: DefaultWorkspaceFormState[K]) => {
      setWorkspaceForm((previous) => ({ ...previous, [field]: value }));
    },
    []
  );

  const refreshAfterStep = useCallback(async () => {
    await refreshSetupConsole();
    await onSnapshotChanged();
  }, [onSnapshotChanged, refreshSetupConsole]);

  const guardedRun = useCallback(
    async (step: SetupConsoleStep, executor: () => Promise<void>) => {
      if (busyStep) {
        return;
      }
      setBusyStep(step);
      setErrorMessage(null);
      try {
        await executor();
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : t("setupConsoleStepStateFailed"));
      } finally {
        setBusyStep(null);
        await refreshAfterStep();
      }
    },
    [busyStep, refreshAfterStep, t]
  );

  const runPrecheck = useCallback(
    () =>
      guardedRun("precheck", async () => {
        await precheckSystem({});
      }),
    [guardedRun]
  );

  const runSchema = useCallback(
    () =>
      guardedRun("schema", async () => {
        await initializeSchema({});
      }),
    [guardedRun]
  );

  const runSeed = useCallback(
    () =>
      guardedRun("seed", async () => {
        await seedSystem({});
      }),
    [guardedRun]
  );

  const runBootstrapUser = useCallback(
    () =>
      guardedRun("bootstrap-user", async () => {
        const response = await bootstrapAdminUser({
          username: adminForm.username,
          password: adminForm.password,
          tenantId: adminForm.tenantId,
          isPlatformAdmin: true,
          optionalRoleCodes: adminForm.optionalRoleCodes,
          generateRecoveryKey: adminForm.generateRecoveryKey
        });
        if (response.success && response.data?.recoveryKey) {
          setRecoveryKey(response.data.recoveryKey);
        }
      }),
    [adminForm, guardedRun]
  );

  const runDefaultWorkspace = useCallback(
    () =>
      guardedRun("default-workspace", async () => {
        await bootstrapDefaultWorkspace(workspaceForm);
      }),
    [guardedRun, workspaceForm]
  );

  const runComplete = useCallback(
    () =>
      guardedRun("complete", async () => {
        await completeSystemInit();
      }),
    [guardedRun]
  );

  const retryStep = useCallback(
    (step: SetupConsoleStep) =>
      guardedRun(step, async () => {
        await retrySystemStep(step);
      }),
    [guardedRun]
  );

  if (!system) {
    return (
      <SectionCard testId="setup-console-system-init-loading">
        <Text type="tertiary">{t("loading")}</Text>
      </SectionCard>
    );
  }

  return (
    <div data-testid="setup-console-system-init">
      {isDone ? (
        <div data-testid="setup-console-system-init-done">
          <ResultCard
            status="success"
            title={t("setupConsoleStateCompleted")}
            description={t("setupConsoleSystemAdminCreated")}
          />
        </div>
      ) : null}

      {errorMessage ? (
        <div style={{ marginBottom: 12 }}>
          <InfoBanner
            variant="danger"
            title={t("setupConsoleStepStateFailed")}
            description={errorMessage}
            testId="setup-console-system-init-error"
          />
        </div>
      ) : null}

      {recoveryKey ? (
        <RecoveryKeyDisplay
          recoveryKey={recoveryKey}
          onAcknowledge={() => setRecoveryKey(null)}
        />
      ) : null}

      {STEP_ORDER.map((meta, index) => {
        const record = findRecord(system.steps, meta.step);
        const isCurrent = index === currentStepIndex && !isDone;
        const isLocked = index > currentStepIndex && !isDone && record?.state !== "succeeded";
        const stepBusy = busyStep === meta.step;
        const onRunHandler = pickRunHandler(meta.step, {
          runPrecheck,
          runSchema,
          runSeed,
          runBootstrapUser,
          runDefaultWorkspace,
          runComplete
        });
        const onRetryHandler = () => void retryStep(meta.step);

        return (
          <StepCard
            key={meta.step}
            step={meta.step}
            index={index}
            titleKey={meta.titleKey}
            record={record}
            isCurrent={isCurrent}
            isLocked={isLocked}
            busy={stepBusy || (busyStep !== null && busyStep !== meta.step)}
            onRun={onRunHandler}
            onRetry={onRetryHandler}
          >
            {meta.step === "bootstrap-user" ? (
              <BootstrapUserForm
                form={adminForm}
                disabled={record?.state === "succeeded" || isDone}
                onChangeField={updateAdminField}
              />
            ) : null}
            {meta.step === "default-workspace" ? (
              <DefaultWorkspaceForm
                form={workspaceForm}
                disabled={record?.state === "succeeded" || isDone}
                onChangeField={updateWorkspaceField}
              />
            ) : null}
          </StepCard>
        );
      })}
    </div>
  );
}

interface RunHandlers {
  runPrecheck: () => Promise<void>;
  runSchema: () => Promise<void>;
  runSeed: () => Promise<void>;
  runBootstrapUser: () => Promise<void>;
  runDefaultWorkspace: () => Promise<void>;
  runComplete: () => Promise<void>;
}

function pickRunHandler(step: SetupConsoleStep, handlers: RunHandlers): () => void {
  switch (step) {
    case "precheck":
      return () => void handlers.runPrecheck();
    case "schema":
      return () => void handlers.runSchema();
    case "seed":
      return () => void handlers.runSeed();
    case "bootstrap-user":
      return () => void handlers.runBootstrapUser();
    case "default-workspace":
      return () => void handlers.runDefaultWorkspace();
    case "complete":
    default:
      return () => void handlers.runComplete();
  }
}

interface BootstrapUserFormProps {
  form: AdminFormState;
  disabled: boolean;
  onChangeField: <K extends keyof AdminFormState>(field: K, value: AdminFormState[K]) => void;
}

function FieldStack({ label, children }: { label: React.ReactNode; children: React.ReactNode }) {
  return (
    <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <Text strong>{label}</Text>
      {children}
    </label>
  );
}

function BootstrapUserForm({ form, disabled, onChangeField }: BootstrapUserFormProps) {
  const { t } = useAppI18n();
  const togglesRoleCode = (code: string, checked: boolean) => {
    const set = new Set(form.optionalRoleCodes);
    if (checked) {
      set.add(code);
    } else {
      set.delete(code);
    }
    onChangeField("optionalRoleCodes", Array.from(set));
  };

  return (
    <div
      data-testid="setup-console-bootstrap-user-form"
      style={{ display: "flex", flexDirection: "column", gap: 12 }}
    >
      <FieldStack label={t("setupConsoleSystemAdminTenantIdLabel")}>
        <Input
          data-testid="setup-console-bootstrap-user-tenant"
          disabled={disabled}
          value={form.tenantId}
          onChange={(value) => onChangeField("tenantId", value)}
        />
      </FieldStack>
      <FieldStack label={t("setupConsoleSystemAdminUsernameLabel")}>
        <Input
          data-testid="setup-console-bootstrap-user-username"
          disabled={disabled}
          value={form.username}
          onChange={(value) => onChangeField("username", value)}
        />
      </FieldStack>
      <FieldStack label={t("setupConsoleSystemAdminPasswordLabel")}>
        <Input
          mode="password"
          data-testid="setup-console-bootstrap-user-password"
          disabled={disabled}
          value={form.password}
          onChange={(value) => onChangeField("password", value)}
        />
      </FieldStack>

      <label style={{ display: "inline-flex", alignItems: "center", gap: 8 }}>
        <Checkbox
          data-testid="setup-console-bootstrap-user-generate-recovery"
          disabled={disabled}
          checked={form.generateRecoveryKey}
          onChange={(event) => onChangeField("generateRecoveryKey", Boolean(event.target.checked))}
        />
        <Text>{t("setupConsoleSystemAdminGenerateRecoveryLabel")}</Text>
      </label>

      <div>
        <Title heading={6} style={{ margin: "8px 0" }}>
          {t("setupOptionalRolesTitle")}
        </Title>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(180px, 1fr))",
            gap: 8
          }}
        >
          {OPTIONAL_ROLE_CODES.map((code) => {
            const checked = form.optionalRoleCodes.includes(code);
            return (
              <label
                key={code}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 8,
                  padding: "8px 12px",
                  borderRadius: 8,
                  border: `1px solid ${
                    checked ? "var(--semi-color-primary)" : "var(--semi-color-border)"
                  }`,
                  background: checked ? "var(--semi-color-primary-light-default)" : "transparent",
                  cursor: disabled ? "not-allowed" : "pointer"
                }}
              >
                <Checkbox
                  data-testid={`setup-console-bootstrap-user-role-${code}`}
                  disabled={disabled}
                  checked={checked}
                  onChange={(event) => togglesRoleCode(code, Boolean(event.target.checked))}
                />
                <Text>{code}</Text>
              </label>
            );
          })}
        </div>
      </div>
    </div>
  );
}

interface DefaultWorkspaceFormProps {
  form: DefaultWorkspaceFormState;
  disabled: boolean;
  onChangeField: <K extends keyof DefaultWorkspaceFormState>(field: K, value: DefaultWorkspaceFormState[K]) => void;
}

function DefaultWorkspaceForm({ form, disabled, onChangeField }: DefaultWorkspaceFormProps) {
  const { t } = useAppI18n();
  return (
    <div
      data-testid="setup-console-default-workspace-form"
      style={{ display: "flex", flexDirection: "column", gap: 12 }}
    >
      <FieldStack label={t("setupConsoleSystemDefaultWorkspaceNameLabel")}>
        <Input
          data-testid="setup-console-default-workspace-name"
          disabled={disabled}
          value={form.workspaceName}
          onChange={(value) => onChangeField("workspaceName", value)}
        />
      </FieldStack>
      <FieldStack label={t("setupConsoleSystemDefaultWorkspaceOwnerLabel")}>
        <Input
          data-testid="setup-console-default-workspace-owner"
          disabled={disabled}
          value={form.ownerUsername}
          onChange={(value) => onChangeField("ownerUsername", value)}
        />
      </FieldStack>
      <label style={{ display: "inline-flex", alignItems: "center", gap: 8 }}>
        <Checkbox
          data-testid="setup-console-default-workspace-channels"
          disabled={disabled}
          checked={form.applyDefaultPublishChannels}
          onChange={(event) => onChangeField("applyDefaultPublishChannels", Boolean(event.target.checked))}
        />
        <Text>{t("setupConsoleSystemDefaultWorkspaceApplyChannelsLabel")}</Text>
      </label>
      <label style={{ display: "inline-flex", alignItems: "center", gap: 8 }}>
        <Checkbox
          data-testid="setup-console-default-workspace-models"
          disabled={disabled}
          checked={form.applyDefaultModelStub}
          onChange={(event) => onChangeField("applyDefaultModelStub", Boolean(event.target.checked))}
        />
        <Text>{t("setupConsoleSystemDefaultWorkspaceApplyModelStubLabel")}</Text>
      </label>
    </div>
  );
}

import { useAppI18n } from "../../../i18n";
import type { AppMessageKey } from "../../../messages";
import type { SetupStepRecordDto } from "../../../../services/api-setup-console";
import type { SetupConsoleStep } from "../../../setup-console-state-machine";

interface StepCardProps {
  step: SetupConsoleStep;
  index: number;
  titleKey: AppMessageKey;
  hintKey?: AppMessageKey;
  record: SetupStepRecordDto | null;
  isCurrent: boolean;
  isLocked: boolean;
  busy: boolean;
  onRun?: () => void;
  onRetry?: () => void;
  children?: React.ReactNode;
}

const STEP_STATE_LABEL_KEY: Record<SetupStepRecordDto["state"], AppMessageKey> = {
  running: "setupConsoleStepStateRunning",
  succeeded: "setupConsoleStepStateSucceeded",
  failed: "setupConsoleStepStateFailed",
  skipped: "setupConsoleStepStateSkipped"
};

const STEP_STATE_VARIANT: Record<SetupStepRecordDto["state"], string> = {
  running: "is-info",
  succeeded: "is-success",
  failed: "is-error",
  skipped: ""
};

export function StepCard({
  step,
  index,
  titleKey,
  hintKey,
  record,
  isCurrent,
  isLocked,
  busy,
  onRun,
  onRetry,
  children
}: StepCardProps) {
  const { t } = useAppI18n();
  const stateLabel = record ? t(STEP_STATE_LABEL_KEY[record.state]) : "";
  const stateVariant = record ? STEP_STATE_VARIANT[record.state] : "";
  const failed = record?.state === "failed";
  const succeeded = record?.state === "succeeded";

  return (
    <section
      className={`atlas-setup-panel ${isCurrent ? "is-current" : ""} ${isLocked ? "is-locked" : ""}`.trim()}
      data-testid={`setup-console-step-${step}`}
      aria-current={isCurrent ? "step" : undefined}
    >
      <div className="atlas-org-section__header">
        <div>
          <div className="atlas-section-title">
            <span className="atlas-step-index" aria-hidden="true">
              {index + 1}
            </span>
            <span style={{ marginLeft: 8 }}>{t(titleKey)}</span>
          </div>
          {hintKey ? <div className="atlas-field-hint">{t(hintKey)}</div> : null}
        </div>
        {record ? (
          <span
            className={`atlas-pill ${stateVariant}`.trim()}
            data-testid={`setup-console-step-${step}-badge`}
          >
            {stateLabel}
          </span>
        ) : null}
      </div>

      {failed && record?.errorMessage ? (
        <div
          className="atlas-warning-banner atlas-warning-banner--compact"
          data-testid={`setup-console-step-${step}-error`}
        >
          <strong>{t("setupConsoleStepStateFailed")}</strong>
          <p>{record.errorMessage}</p>
        </div>
      ) : null}

      {children}

      <div className="atlas-setup-actions">
        <span />
        <div style={{ display: "flex", gap: 8 }}>
          {failed ? (
            <button
              type="button"
              className="atlas-button atlas-button--secondary"
              data-testid={`setup-console-step-${step}-retry`}
              disabled={busy}
              onClick={onRetry}
            >
              {t("setupConsoleSystemRetry")}
            </button>
          ) : null}
          {onRun ? (
            <button
              type="button"
              className="atlas-button atlas-button--primary"
              data-testid={`setup-console-step-${step}-run`}
              disabled={busy || isLocked || succeeded}
              onClick={onRun}
            >
              {succeeded
                ? t("setupConsoleStepStateSucceeded")
                : busy && isCurrent
                  ? t("setupConsoleStepStateRunning")
                  : t("setupConsoleSystemResume")}
            </button>
          ) : null}
        </div>
      </div>
    </section>
  );
}

import { Button, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../../../i18n";
import type { AppMessageKey } from "../../../messages";
import type { SetupStepRecordDto } from "../../../../services/api-setup-console";
import type { SetupConsoleStep } from "../../../setup-console-state-machine";
import {
  InfoBanner,
  SectionCard,
  StateBadge,
  type StateBadgeVariant
} from "../../../_shared";

const { Text } = Typography;

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

const STEP_STATE_VARIANT: Record<SetupStepRecordDto["state"], StateBadgeVariant> = {
  running: "info",
  succeeded: "success",
  failed: "danger",
  skipped: "neutral"
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
  const failed = record?.state === "failed";
  const succeeded = record?.state === "succeeded";

  const stepNumber = (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        width: 24,
        height: 24,
        borderRadius: "50%",
        background: isCurrent ? "var(--semi-color-primary)" : "var(--semi-color-fill-2)",
        color: isCurrent ? "#fff" : "var(--semi-color-text-2)",
        fontSize: 12,
        fontWeight: 600,
        marginRight: 8
      }}
      aria-hidden="true"
    >
      {index + 1}
    </span>
  );

  return (
    <div
      data-testid={`setup-console-step-${step}`}
      aria-current={isCurrent ? "step" : undefined}
      style={{ opacity: isLocked ? 0.6 : 1 }}
    >
      <SectionCard
        title={
          <span style={{ display: "inline-flex", alignItems: "center" }}>
            {stepNumber}
            <span>{t(titleKey)}</span>
          </span>
        }
        subtitle={hintKey ? t(hintKey) : undefined}
        actions={
          record ? (
            <StateBadge
              variant={STEP_STATE_VARIANT[record.state]}
              testId={`setup-console-step-${step}-badge`}
            >
              {t(STEP_STATE_LABEL_KEY[record.state])}
            </StateBadge>
          ) : null
        }
      >
        {failed && record?.errorMessage ? (
          <div style={{ marginBottom: 12 }}>
            <InfoBanner
              variant="danger"
              compact
              title={t("setupConsoleStepStateFailed")}
              description={record.errorMessage}
              testId={`setup-console-step-${step}-error`}
            />
          </div>
        ) : null}

        {children}

        <div
          style={{
            marginTop: 16,
            display: "flex",
            justifyContent: "flex-end",
            gap: 8,
            alignItems: "center"
          }}
        >
          {failed ? (
            <Button
              type="tertiary"
              theme="light"
              data-testid={`setup-console-step-${step}-retry`}
              disabled={busy}
              onClick={onRetry}
            >
              {t("setupConsoleSystemRetry")}
            </Button>
          ) : null}
          {onRun ? (
            <Button
              type="primary"
              theme="solid"
              data-testid={`setup-console-step-${step}-run`}
              disabled={busy || isLocked || succeeded}
              loading={busy && isCurrent}
              onClick={onRun}
            >
              {succeeded
                ? t("setupConsoleStepStateSucceeded")
                : busy && isCurrent
                  ? t("setupConsoleStepStateRunning")
                  : t("setupConsoleSystemResume")}
            </Button>
          ) : (
            <Text type="tertiary" style={{ fontSize: 12 }}>
              {isLocked ? "" : ""}
            </Text>
          )}
        </div>
      </SectionCard>
    </div>
  );
}

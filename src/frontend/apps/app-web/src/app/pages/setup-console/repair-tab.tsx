import { useCallback, useState } from "react";
import { Button } from "@douyinfe/semi-ui";
import { useAppI18n } from "../../i18n";
import { useBootstrap } from "../../bootstrap-context";
import { setupConsoleApi } from "../../../services/api-setup-console";
import { InfoBanner, SectionCard } from "../../_shared";
import { RecoveryKeyDisplay } from "./components/recovery-key-display";

/**
 * 控制台 Repair Tab：暴露运维场景需要的高阶动作。
 */
export function RepairTab() {
  const { t } = useAppI18n();
  const { refreshSetupConsole } = useBootstrap();
  const [busy, setBusy] = useState<string | null>(null);
  const [recoveryKey, setRecoveryKey] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const guarded = useCallback(
    async (label: string, executor: () => Promise<string | null>) => {
      if (busy) return;
      setBusy(label);
      setMessage(null);
      try {
        const result = await executor();
        setMessage(result);
      } catch (error) {
        setMessage(error instanceof Error ? error.message : t("setupConsoleStepStateFailed"));
      } finally {
        setBusy(null);
        await refreshSetupConsole();
      }
    },
    [busy, refreshSetupConsole, t]
  );

  const handleReopen = () =>
    guarded("reopen", async () => {
      await setupConsoleApi.systemReopen();
      return t("setupConsoleSystemResume");
    });

  const handleForceReseed = () =>
    guarded("reseed", async () => {
      await setupConsoleApi.systemSeed({ bundleVersion: "v2", forceReapply: true });
      return `${t("setupConsoleStepSeed")} v2`;
    });

  const handleRegenerateRecoveryKey = () =>
    guarded("regenerate-recovery", async () => {
      const response = await setupConsoleApi.systemBootstrapUser({
        username: "admin",
        password: "P@ssw0rd!",
        tenantId: "00000000-0000-0000-0000-000000000001",
        isPlatformAdmin: true,
        optionalRoleCodes: [],
        generateRecoveryKey: true
      });
      if (response.success && response.data?.recoveryKey) {
        setRecoveryKey(response.data.recoveryKey);
      }
      return null;
    });

  return (
    <div data-testid="setup-console-repair">
      {message ? (
        <div style={{ marginBottom: 12 }}>
          <InfoBanner
            variant="info"
            compact
            description={message}
            testId="setup-console-repair-message"
          />
        </div>
      ) : null}

      {recoveryKey ? (
        <RecoveryKeyDisplay
          recoveryKey={recoveryKey}
          onAcknowledge={() => setRecoveryKey(null)}
        />
      ) : null}

      <div data-testid="setup-console-repair-actions">
        <SectionCard title={t("setupConsoleTabRepair")}>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
            <Button
              type="tertiary"
              theme="light"
              data-testid="setup-console-repair-reopen"
              disabled={busy !== null}
              loading={busy === "reopen"}
              onClick={() => void handleReopen()}
            >
              {t("setupConsoleSystemReopen")}
            </Button>
            <Button
              type="tertiary"
              theme="light"
              data-testid="setup-console-repair-reseed"
              disabled={busy !== null}
              loading={busy === "reseed"}
              onClick={() => void handleForceReseed()}
            >
              {t("setupConsoleStepSeed")} v2
            </Button>
            <Button
              type="primary"
              theme="solid"
              data-testid="setup-console-repair-regenerate-recovery"
              disabled={busy !== null}
              loading={busy === "regenerate-recovery"}
              onClick={() => void handleRegenerateRecoveryKey()}
            >
              {t("setupConsoleSystemAdminGenerateRecoveryLabel")}
            </Button>
          </div>
        </SectionCard>
      </div>
    </div>
  );
}

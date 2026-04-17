import { useCallback, useState } from "react";
import { useAppI18n } from "../../i18n";
import { useBootstrap } from "../../bootstrap-context";
import {
  bootstrapAdminUser,
  reopenSystemConsole,
  seedSystem
} from "../../../services/mock";
import { setUseRealConsoleApi, shouldUseRealConsoleApi } from "../../../services/mock/mock-switch";
import { RecoveryKeyDisplay } from "./components/recovery-key-display";

/**
 * 控制台 Repair Tab（M10/D8）：暴露运维场景需要的 4 个高阶动作 + 1 个 mock/real 开关。
 *
 * - 重新打开初始化（dismissed → not_started）
 * - 强制重置 v1→v2（forceReapply seed bundle）
 * - 重新生成恢复密钥（不动 admin 用户，只重派发 recovery key）
 * - mock/real 切换（D6 配套 UI；默认 mock）
 */
export function RepairTab() {
  const { t } = useAppI18n();
  const { refreshSetupConsole } = useBootstrap();
  const [busy, setBusy] = useState<string | null>(null);
  const [recoveryKey, setRecoveryKey] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [useReal, setUseReal] = useState<boolean>(shouldUseRealConsoleApi());

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
      await reopenSystemConsole();
      return t("setupConsoleSystemResume");
    });

  const handleForceReseed = () =>
    guarded("reseed", async () => {
      await seedSystem({ bundleVersion: "v2", forceReapply: true });
      return `${t("setupConsoleStepSeed")} v2`;
    });

  const handleRegenerateRecoveryKey = () =>
    guarded("regenerate-recovery", async () => {
      const response = await bootstrapAdminUser({
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

  const handleToggleReal = (next: boolean) => {
    setUseReal(next);
    setUseRealConsoleApi(next);
    setMessage(next ? "real" : "mock");
  };

  return (
    <div data-testid="setup-console-repair">
      {message ? (
        <div className="atlas-info-banner" data-testid="setup-console-repair-message">
          {message}
        </div>
      ) : null}

      {recoveryKey ? (
        <RecoveryKeyDisplay
          recoveryKey={recoveryKey}
          onAcknowledge={() => setRecoveryKey(null)}
        />
      ) : null}

      <section className="atlas-setup-panel" data-testid="setup-console-repair-actions">
        <div className="atlas-section-title">{t("setupConsoleTabRepair")}</div>

        <div className="atlas-setup-actions" style={{ flexWrap: "wrap", gap: 8, justifyContent: "flex-start" }}>
          <button
            type="button"
            className="atlas-button atlas-button--secondary"
            data-testid="setup-console-repair-reopen"
            disabled={busy !== null}
            onClick={() => void handleReopen()}
          >
            {t("setupConsoleSystemReopen")}
          </button>
          <button
            type="button"
            className="atlas-button atlas-button--secondary"
            data-testid="setup-console-repair-reseed"
            disabled={busy !== null}
            onClick={() => void handleForceReseed()}
          >
            {t("setupConsoleStepSeed")} v2
          </button>
          <button
            type="button"
            className="atlas-button atlas-button--primary"
            data-testid="setup-console-repair-regenerate-recovery"
            disabled={busy !== null}
            onClick={() => void handleRegenerateRecoveryKey()}
          >
            {t("setupConsoleSystemAdminGenerateRecoveryLabel")}
          </button>
        </div>
      </section>

      <section className="atlas-setup-panel" data-testid="setup-console-repair-mock-switch">
        <div className="atlas-section-title">Mock / Real backend</div>
        <label className="atlas-form-field" style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
          <input
            type="checkbox"
            data-testid="setup-console-repair-toggle-real"
            checked={useReal}
            onChange={(event) => handleToggleReal(event.target.checked)}
          />
          <span>{useReal ? "Use real backend" : "Use mock"}</span>
        </label>
        <p className="atlas-field-hint">
          {useReal
            ? "Console 调用走真实 setupConsoleApi（M5 后端落地后开启）。"
            : "Console 走前端 mock，方便开发与 Playwright 测试。"}
        </p>
      </section>
    </div>
  );
}

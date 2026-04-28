import { useCallback, useState } from "react";
import { Button, Switch, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../../i18n";
import { useBootstrap } from "../../bootstrap-context";
import {
  bootstrapAdminUser,
  reopenSystemConsole,
  seedSystem
} from "../../../services/mock";
import { setUseRealConsoleApi, shouldUseRealConsoleApi } from "../../../services/mock/mock-switch";
import { InfoBanner, SectionCard } from "../../_shared";
import { RecoveryKeyDisplay } from "./components/recovery-key-display";

const { Text } = Typography;

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

      <div data-testid="setup-console-repair-mock-switch">
        <SectionCard title="Mock / Real backend">
          <label style={{ display: "inline-flex", alignItems: "center", gap: 8 }}>
            <Switch
              data-testid="setup-console-repair-toggle-real"
              checked={useReal}
              onChange={(checked) => handleToggleReal(Boolean(checked))}
            />
            <Text>{useReal ? "Use real backend" : "Use mock"}</Text>
          </label>
          <Text type="tertiary" style={{ display: "block", marginTop: 8, fontSize: 12 }}>
            {useReal
              ? "Console 调用走真实 setupConsoleApi（M5 后端落地后开启）。"
              : "Console 走前端 mock，方便开发与 Playwright 测试。"}
          </Text>
        </SectionCard>
      </div>
    </div>
  );
}

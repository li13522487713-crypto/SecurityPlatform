import { useState } from "react";
import { Button, Checkbox, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../../../i18n";
import { InfoBanner } from "../../../_shared";

const { Text } = Typography;

interface RecoveryKeyDisplayProps {
  recoveryKey: string;
  onAcknowledge: () => void;
}

/**
 * 恢复密钥一次性展示卡片。
 *
 * - 用户必须显式 acknowledge 才能继续；
 * - 提供"复制到剪贴板"按钮（兼容 navigator.clipboard 缺失的场景，回退 textarea + execCommand）。
 */
export function RecoveryKeyDisplay({ recoveryKey, onAcknowledge }: RecoveryKeyDisplayProps) {
  const { t } = useAppI18n();
  const [copied, setCopied] = useState(false);
  const [acknowledged, setAcknowledged] = useState(false);

  const handleCopy = async () => {
    try {
      if (typeof navigator !== "undefined" && navigator.clipboard) {
        await navigator.clipboard.writeText(recoveryKey);
        setCopied(true);
        return;
      }
    } catch {
      // 进入 fallback
    }

    if (typeof document === "undefined") {
      return;
    }

    const textarea = document.createElement("textarea");
    textarea.value = recoveryKey;
    textarea.setAttribute("readonly", "");
    textarea.style.position = "absolute";
    textarea.style.left = "-9999px";
    document.body.appendChild(textarea);
    textarea.select();
    try {
      document.execCommand("copy");
      setCopied(true);
    } catch {
      setCopied(false);
    } finally {
      document.body.removeChild(textarea);
    }
  };

  return (
    <div style={{ marginBottom: 12 }} data-testid="setup-console-recovery-key-display">
      <InfoBanner
        variant="warning"
        title={t("setupConsoleSystemRecoveryKeyHint")}
        description={
          <div style={{ display: "flex", flexDirection: "column", gap: 12, marginTop: 8 }}>
            <div
              style={{
                padding: 12,
                borderRadius: 8,
                background: "var(--semi-color-text-0)",
                color: "var(--semi-color-bg-0)",
                fontFamily: "monospace",
                fontSize: 18,
                letterSpacing: "0.08em",
                textAlign: "center",
                userSelect: "all"
              }}
              data-testid="setup-console-recovery-key-value"
            >
              {recoveryKey}
            </div>

            <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 12 }}>
              <Button
                type="tertiary"
                theme="light"
                data-testid="setup-console-recovery-key-copy"
                onClick={() => void handleCopy()}
              >
                {copied ? t("setupConsoleSystemRecoveryKeyCopied") : t("setupConsoleSystemRecoveryKeyCopy")}
              </Button>
              <label style={{ display: "inline-flex", alignItems: "center", gap: 8 }}>
                <Checkbox
                  checked={acknowledged}
                  data-testid="setup-console-recovery-key-acknowledge"
                  onChange={(event) => setAcknowledged(Boolean(event.target.checked))}
                />
                <Text>{t("setupConsoleSystemRecoveryKeyHint")}</Text>
              </label>
              <Button
                type="primary"
                theme="solid"
                data-testid="setup-console-recovery-key-confirm"
                disabled={!acknowledged}
                onClick={onAcknowledge}
              >
                {t("setupConsoleAuthSubmit")}
              </Button>
            </div>
          </div>
        }
      />
    </div>
  );
}

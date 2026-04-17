import { useState } from "react";
import { useAppI18n } from "../../../i18n";

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
    <div className="atlas-warning-banner" data-testid="setup-console-recovery-key-display">
      <strong>{t("setupConsoleSystemRecoveryKeyHint")}</strong>
      <div
        style={{
          marginTop: 12,
          padding: 12,
          borderRadius: 8,
          background: "#1f1f1f",
          color: "#fafafa",
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

      <div className="atlas-setup-actions" style={{ marginTop: 12, justifyContent: "flex-start", gap: 8 }}>
        <button
          type="button"
          className="atlas-button atlas-button--secondary"
          data-testid="setup-console-recovery-key-copy"
          onClick={() => void handleCopy()}
        >
          {copied ? t("setupConsoleSystemRecoveryKeyCopied") : t("setupConsoleSystemRecoveryKeyCopy")}
        </button>
        <label className="atlas-form-field" style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
          <input
            type="checkbox"
            checked={acknowledged}
            data-testid="setup-console-recovery-key-acknowledge"
            onChange={(event) => setAcknowledged(event.target.checked)}
          />
          <span>{t("setupConsoleSystemRecoveryKeyHint")}</span>
        </label>
        <button
          type="button"
          className="atlas-button atlas-button--primary"
          data-testid="setup-console-recovery-key-confirm"
          disabled={!acknowledged}
          onClick={onAcknowledge}
        >
          {t("setupConsoleAuthSubmit")}
        </button>
      </div>
    </div>
  );
}

import { useState } from "react";
import { Button, Card, Input, Space, Toast, Typography } from "@douyinfe/semi-ui";
import { IconArrowRight } from "@douyinfe/semi-icons";
import { getMendixStudioCopy } from "./i18n/copy";

const { Text } = Typography;

const MENDIX_STUDIO_DEV_SAMPLE_APP_ID = "app_procurement";

/**
 * Whether the dev-sample card is shown on the Mendix Studio landing page.
 * 默认仅在本地 dev build 显示；测试可通过 `setMendixStudioDevSampleEnabledForTesting` 注入。
 */
let __devSampleEnabledOverride: boolean | undefined;

export function setMendixStudioDevSampleEnabledForTesting(enabled: boolean | undefined): void {
  __devSampleEnabledOverride = enabled;
}

function isMendixStudioDevSampleEnabled(): boolean {
  if (typeof __devSampleEnabledOverride === "boolean") {
    return __devSampleEnabledOverride;
  }
  try {
    return Boolean((import.meta as { env?: { DEV?: boolean } }).env?.DEV);
  } catch {
    return false;
  }
}

export interface MendixStudioIndexPageProps {
  workspaceId: string;
  onOpen: (appId: string) => void;
}

export function MendixStudioIndexPage({ workspaceId, onOpen }: MendixStudioIndexPageProps) {
  const copy = getMendixStudioCopy();
  const [appIdInput, setAppIdInput] = useState("");
  const showDevSample = isMendixStudioDevSampleEnabled();

  const handleOpen = () => {
    const trimmed = appIdInput.trim();
    if (!trimmed) {
      Toast.warning({ content: copy.index.openAppEmptyError, duration: 2 });
      return;
    }
    onOpen(trimmed);
  };

  return (
    <div
      style={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        background: "linear-gradient(135deg, #f0f4ff 0%, #e8f3ff 100%)",
      }}
    >
      <Card
        style={{
          width: 480,
          boxShadow: "0 20px 60px rgba(0,0,0,0.12)",
          borderRadius: 12,
          border: "none",
        }}
        bodyStyle={{ padding: "40px 40px 32px" }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 24 }}>
          <div
            style={{
              width: 36,
              height: 36,
              background: "#1677ff",
              borderRadius: 8,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: "#fff",
              fontWeight: 800,
              fontSize: 16,
            }}
          >
            mx
          </div>
          <div>
            <div style={{ fontWeight: 700, fontSize: 18, color: "#1c2a3a" }}>Lowcode Studio</div>
            <div style={{ fontSize: 12, color: "#6b7280" }}>Mendix-compatible low-code IDE</div>
          </div>
        </div>

        <Text type="tertiary" style={{ display: "block", marginBottom: 8, fontSize: 13 }}>
          {copy.index.workspaceLabel}: <strong style={{ color: "#374151" }}>{workspaceId}</strong>
        </Text>

        <div style={{ marginBottom: 16 }}>
          <div style={{ fontWeight: 600, fontSize: 14, color: "#1c2a3a", marginBottom: 6 }}>
            {copy.index.noAppTitle}
          </div>
          <Text type="tertiary" size="small">
            {copy.index.noAppDescription}
          </Text>
        </div>

        <Space vertical style={{ width: "100%" }} spacing={12}>
          <Input
            value={appIdInput}
            onChange={value => setAppIdInput(value)}
            placeholder={copy.index.openAppPlaceholder}
            onEnterPress={handleOpen}
            data-testid="mendix-studio-app-id-input"
          />
          <Button
            theme="solid"
            type="primary"
            block
            style={{ height: 40, fontSize: 14 }}
            onClick={handleOpen}
            data-testid="mendix-studio-open-app-button"
          >
            {copy.index.openAppButton}
          </Button>
          <Button
            theme="light"
            type="primary"
            block
            style={{ height: 40, fontSize: 14 }}
            onClick={() => {
              Toast.info({ content: copy.index.createAppInProgress, duration: 2 });
            }}
          >
            {copy.index.createAppButton}
          </Button>

          {showDevSample ? (
            <div
              data-testid="mendix-studio-dev-sample-card"
              style={{
                border: "1px dashed #cbd5e1",
                borderRadius: 8,
                padding: "12px 16px",
                cursor: "pointer",
                background: "#f8fafc",
              }}
              onClick={() => onOpen(MENDIX_STUDIO_DEV_SAMPLE_APP_ID)}
            >
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                <div>
                  <div style={{ fontWeight: 600, fontSize: 13, color: "#1c2a3a", marginBottom: 4 }}>
                    {copy.index.devSampleHint}
                  </div>
                  <div style={{ fontSize: 12, color: "#6b7280" }}>
                    {copy.index.sampleAppDescription}
                  </div>
                </div>
                <IconArrowRight style={{ color: "#1677ff", fontSize: 16, flexShrink: 0 }} />
              </div>
            </div>
          ) : null}
        </Space>

        <div style={{ marginTop: 24, paddingTop: 20, borderTop: "1px solid #f0f2f5" }}>
          <Text type="tertiary" size="small">
            {copy.index.footer}
          </Text>
        </div>
      </Card>
    </div>
  );
}

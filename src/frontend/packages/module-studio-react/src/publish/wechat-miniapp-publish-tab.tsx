import { Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";
import { getStudioCopy } from "../copy";
import {
  ChannelCredentialFormCard,
  type ChannelCredentialField,
  type ChannelCredentialSummaryItem
} from "./channel-credential-form-card";

export interface WechatMiniappPublishTabProps {
  workspaceId: string;
  channelId: string;
  locale: StudioLocale;
  fetcher: <T = unknown>(input: { url: string; method: string; body?: unknown }) => Promise<T>;
  webhookUrl?: string;
  testId?: string;
}

interface WechatMiniappCredentialDto {
  id: string;
  channelId: string;
  workspaceId: string;
  appId: string;
  appIdMasked: string;
  originalId: string;
  messageToken: string;
  hasEncodingAesKey: boolean;
  accessTokenExpiresAt: string | null;
  refreshCount: number;
  createdAt: string;
  updatedAt: string;
}

export function WechatMiniappPublishTab({
  workspaceId,
  channelId,
  locale,
  fetcher,
  webhookUrl,
  testId = "studio-publish-wechat-miniapp-tab"
}: WechatMiniappPublishTabProps) {
  const copy = getStudioCopy(locale);
  const baseUrl = `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/publish-channels/${encodeURIComponent(channelId)}/wechat-miniapp-credential`;
  const fields: ReadonlyArray<ChannelCredentialField<WechatMiniappCredentialDto>> = [
    {
      field: "appId",
      label: copy.wechatMiniappTab.appIdLabel,
      required: true,
      initValue: (credential) => credential?.appId ?? ""
    },
    {
      field: "appSecret",
      label: copy.wechatMiniappTab.formAppSecretLabel,
      type: "password",
      required: true
    },
    {
      field: "originalId",
      label: copy.wechatMiniappTab.originalIdLabel,
      initValue: (credential) => credential?.originalId ?? ""
    },
    {
      field: "messageToken",
      label: copy.wechatMiniappTab.messageTokenLabel,
      initValue: (credential) => credential?.messageToken ?? ""
    },
    {
      field: "encodingAesKey",
      label: copy.wechatMiniappTab.formEncodingAesKeyOptionalLabel,
      type: "password"
    }
  ];
  const summaries: ReadonlyArray<ChannelCredentialSummaryItem<WechatMiniappCredentialDto>> = [
    {
      key: "appId",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatMiniappTab.appIdLabel}: <strong>{credential.appId}</strong>
          <span style={{ marginLeft: 8 }}>({credential.appIdMasked})</span>
        </Typography.Text>
      )
    },
    {
      key: "originalId",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatMiniappTab.originalIdLabel}: {credential.originalId || "—"}
        </Typography.Text>
      )
    },
    {
      key: "messageToken",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatMiniappTab.messageTokenLabel}: {credential.messageToken || "—"}
        </Typography.Text>
      )
    },
    {
      key: "aes",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatMiniappTab.encodingAesKeyLabel}: {credential.hasEncodingAesKey ? copy.wechatMiniappTab.encodingAesKeyConfigured : copy.wechatMiniappTab.encodingAesKeyNotSet}
        </Typography.Text>
      )
    },
    {
      key: "refreshCount",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatMiniappTab.accessTokenRefreshCountLabel}: {credential.refreshCount}
        </Typography.Text>
      )
    },
    {
      key: "expiresAt",
      render: (credential) =>
        credential.accessTokenExpiresAt ? (
          <Typography.Text type="tertiary" size="small">
            {copy.wechatMiniappTab.accessTokenExpiresAtLabel}: {credential.accessTokenExpiresAt}
          </Typography.Text>
        ) : null
    }
  ];

  return (
    <ChannelCredentialFormCard<WechatMiniappCredentialDto>
      title={copy.wechatMiniappTab.title}
      hint={copy.wechatMiniappTab.hint}
      loadingText={copy.wechatMiniappTab.loading}
      saveSuccessText={copy.wechatMiniappTab.credentialSaved}
      clearSuccessText={copy.wechatMiniappTab.credentialCleared}
      saveButtonText={copy.wechatMiniappTab.saveCredential}
      clearButtonText={copy.wechatMiniappTab.clear}
      baseUrl={baseUrl}
      fetcher={fetcher}
      fields={fields}
      summaries={summaries}
      buildSubmitBody={(values) => ({
        appId: String(values.appId ?? "").trim(),
        appSecret: String(values.appSecret ?? ""),
        originalId: String(values.originalId ?? "").trim() || null,
        messageToken: String(values.messageToken ?? "").trim() || null,
        encodingAesKey: values.encodingAesKey ? String(values.encodingAesKey) : null
      })}
      webhookUrl={webhookUrl}
      webhookHint={copy.wechatMiniappTab.webhookHint}
      testId={testId}
    />
  );
}

import { Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";
import { getStudioCopy } from "../copy";
import {
  ChannelCredentialFormCard,
  type ChannelCredentialField,
  type ChannelCredentialSummaryItem
} from "./channel-credential-form-card";

export interface WechatCsPublishTabProps {
  workspaceId: string;
  channelId: string;
  locale: StudioLocale;
  fetcher: <T = unknown>(input: { url: string; method: string; body?: unknown }) => Promise<T>;
  webhookUrl?: string;
  testId?: string;
}

interface WechatCsCredentialDto {
  id: string;
  channelId: string;
  workspaceId: string;
  corpId: string;
  corpIdMasked: string;
  openKfId: string;
  token: string;
  hasEncodingAesKey: boolean;
  accessTokenExpiresAt: string | null;
  refreshCount: number;
  createdAt: string;
  updatedAt: string;
}

export function WechatCsPublishTab({
  workspaceId,
  channelId,
  locale,
  fetcher,
  webhookUrl,
  testId = "studio-publish-wechat-cs-tab"
}: WechatCsPublishTabProps) {
  const copy = getStudioCopy(locale);
  const baseUrl = `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/publish-channels/${encodeURIComponent(channelId)}/wechat-cs-credential`;
  const fields: ReadonlyArray<ChannelCredentialField<WechatCsCredentialDto>> = [
    {
      field: "corpId",
      label: copy.wechatCsTab.corpIdLabel,
      required: true,
      initValue: (credential) => credential?.corpId ?? ""
    },
    {
      field: "secret",
      label: copy.wechatCsTab.formSecretLabel,
      type: "password",
      required: true
    },
    {
      field: "openKfId",
      label: copy.wechatCsTab.openKfIdLabel,
      required: true,
      initValue: (credential) => credential?.openKfId ?? ""
    },
    {
      field: "token",
      label: copy.wechatCsTab.serverTokenLabel,
      initValue: (credential) => credential?.token ?? ""
    },
    {
      field: "encodingAesKey",
      label: copy.wechatCsTab.formEncodingAesKeyOptionalLabel,
      type: "password"
    }
  ];
  const summaries: ReadonlyArray<ChannelCredentialSummaryItem<WechatCsCredentialDto>> = [
    {
      key: "corpId",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatCsTab.corpIdLabel}: <strong>{credential.corpId}</strong>
          <span style={{ marginLeft: 8 }}>({credential.corpIdMasked})</span>
        </Typography.Text>
      )
    },
    {
      key: "openKfId",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatCsTab.openKfIdLabel}: {credential.openKfId}
        </Typography.Text>
      )
    },
    {
      key: "token",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatCsTab.serverTokenLabel}: {credential.token || "—"}
        </Typography.Text>
      )
    },
    {
      key: "aes",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatCsTab.encodingAesKeyLabel}: {credential.hasEncodingAesKey ? copy.wechatCsTab.encodingAesKeyConfigured : copy.wechatCsTab.encodingAesKeyNotSet}
        </Typography.Text>
      )
    },
    {
      key: "refreshCount",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatCsTab.accessTokenRefreshCountLabel}: {credential.refreshCount}
        </Typography.Text>
      )
    },
    {
      key: "expiresAt",
      render: (credential) =>
        credential.accessTokenExpiresAt ? (
          <Typography.Text type="tertiary" size="small">
            {copy.wechatCsTab.accessTokenExpiresAtLabel}: {credential.accessTokenExpiresAt}
          </Typography.Text>
        ) : null
    }
  ];

  return (
    <ChannelCredentialFormCard<WechatCsCredentialDto>
      title={copy.wechatCsTab.title}
      hint={copy.wechatCsTab.hint}
      loadingText={copy.wechatCsTab.loading}
      saveSuccessText={copy.wechatCsTab.credentialSaved}
      clearSuccessText={copy.wechatCsTab.credentialCleared}
      saveButtonText={copy.wechatCsTab.saveCredential}
      clearButtonText={copy.wechatCsTab.clear}
      baseUrl={baseUrl}
      fetcher={fetcher}
      fields={fields}
      summaries={summaries}
      buildSubmitBody={(values) => ({
        corpId: String(values.corpId ?? "").trim(),
        secret: String(values.secret ?? ""),
        openKfId: String(values.openKfId ?? "").trim(),
        token: String(values.token ?? "").trim() || null,
        encodingAesKey: values.encodingAesKey ? String(values.encodingAesKey) : null
      })}
      webhookUrl={webhookUrl}
      webhookHint={copy.wechatCsTab.webhookHint}
      testId={testId}
    />
  );
}

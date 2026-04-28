import { Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";
import { getStudioCopy } from "../copy";
import {
  ChannelCredentialFormCard,
  type ChannelCredentialField,
  type ChannelCredentialSummaryItem
} from "./channel-credential-form-card";

/**
 * 治理 M-G02-C11（S4）：微信公众号渠道凭据 Tab。
 */
export interface WechatMpPublishTabProps {
  workspaceId: string;
  channelId: string;
  locale: StudioLocale;
  fetcher: <T = unknown>(input: { url: string; method: string; body?: unknown }) => Promise<T>;
  webhookUrl?: string;
  testId?: string;
}

interface WechatMpCredentialDto {
  id: string;
  channelId: string;
  workspaceId: string;
  appId: string;
  appIdMasked: string;
  token: string;
  hasEncodingAesKey: boolean;
  accessTokenExpiresAt: string | null;
  refreshCount: number;
  createdAt: string;
  updatedAt: string;
}

export function WechatMpPublishTab({
  workspaceId,
  channelId,
  locale,
  fetcher,
  webhookUrl,
  testId = "studio-publish-wechat-mp-tab"
}: WechatMpPublishTabProps) {
  const copy = getStudioCopy(locale);
  const baseUrl = `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/publish-channels/${encodeURIComponent(channelId)}/wechat-mp-credential`;
  const fields: ReadonlyArray<ChannelCredentialField<WechatMpCredentialDto>> = [
    {
      field: "appId",
      label: copy.wechatMpTab.appIdLabel,
      required: true,
      initValue: (credential) => credential?.appId ?? ""
    },
    {
      field: "appSecret",
      label: copy.wechatMpTab.formAppSecretLabel,
      type: "password",
      required: true
    },
    {
      field: "token",
      label: copy.wechatMpTab.serverTokenLabel,
      required: true,
      initValue: (credential) => credential?.token ?? ""
    },
    {
      field: "encodingAesKey",
      label: copy.wechatMpTab.formEncodingAesKeyOptionalLabel,
      type: "password"
    }
  ];
  const summaries: ReadonlyArray<ChannelCredentialSummaryItem<WechatMpCredentialDto>> = [
    {
      key: "appId",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatMpTab.appIdLabel}: <strong>{credential.appId}</strong>
          <span style={{ marginLeft: 8 }}>({credential.appIdMasked})</span>
        </Typography.Text>
      )
    },
    {
      key: "token",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatMpTab.serverTokenLabel}: {credential.token}
        </Typography.Text>
      )
    },
    {
      key: "aes",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatMpTab.encodingAesKeyLabel}: {credential.hasEncodingAesKey ? copy.wechatMpTab.encodingAesKeyConfigured : copy.wechatMpTab.encodingAesKeyNotSet}
        </Typography.Text>
      )
    },
    {
      key: "refreshCount",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.wechatMpTab.accessTokenRefreshCountLabel}: {credential.refreshCount}
        </Typography.Text>
      )
    },
    {
      key: "expiresAt",
      render: (credential) =>
        credential.accessTokenExpiresAt ? (
          <Typography.Text type="tertiary" size="small">
            {copy.wechatMpTab.accessTokenExpiresAtLabel}: {credential.accessTokenExpiresAt}
          </Typography.Text>
        ) : null
    }
  ];

  return (
    <ChannelCredentialFormCard<WechatMpCredentialDto>
      title={copy.wechatMpTab.title}
      hint={copy.wechatMpTab.hint}
      loadingText={copy.wechatMpTab.loading}
      saveSuccessText={copy.wechatMpTab.credentialSaved}
      clearSuccessText={copy.wechatMpTab.credentialCleared}
      saveButtonText={copy.wechatMpTab.saveCredential}
      clearButtonText={copy.wechatMpTab.clear}
      baseUrl={baseUrl}
      fetcher={fetcher}
      fields={fields}
      summaries={summaries}
      buildSubmitBody={(values) => ({
        appId: String(values.appId ?? "").trim(),
        appSecret: String(values.appSecret ?? ""),
        token: String(values.token ?? "").trim(),
        encodingAesKey: values.encodingAesKey ? String(values.encodingAesKey) : null
      })}
      webhookUrl={webhookUrl}
      webhookHint={copy.wechatMpTab.webhookHint}
      testId={testId}
    />
  );
}

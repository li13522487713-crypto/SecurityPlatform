import { Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";
import { getStudioCopy } from "../copy";
import {
  ChannelCredentialFormCard,
  type ChannelCredentialField,
  type ChannelCredentialSummaryItem
} from "./channel-credential-form-card";

/**
 * 治理 M-G02-C8（S3）：飞书渠道凭据 Tab。
 *
 * - 通过 props.fetcher 适配宿主的 HTTP 客户端（已带 Bearer + X-Tenant-Id 拼装）；
 * - GET /api/v1/workspaces/{ws}/publish-channels/{channelId}/feishu-credential 加载现状；
 * - PUT 同 URL upsert；
 * - DELETE 同 URL 撤销凭据。
 *
 * 不渲染发布 release 操作；release 走 PublishCenterPage 通用入口或上层组件。
 */
export interface FeishuPublishTabProps {
  workspaceId: string;
  channelId: string;
  locale: StudioLocale;
  /** 适配宿主的 HTTP 调用：返回已解包 ApiResponse.data 的对象。 */
  fetcher: <T = unknown>(input: { url: string; method: string; body?: unknown }) => Promise<T>;
  /** 当前 release 信息（如果有），用于显示 webhookUrl 复制。 */
  webhookUrl?: string;
  testId?: string;
}

interface FeishuCredentialDto {
  id: string;
  channelId: string;
  workspaceId: string;
  appId: string;
  appIdMasked: string;
  verificationToken: string;
  hasEncryptKey: boolean;
  tenantAccessTokenExpiresAt: string | null;
  refreshCount: number;
  createdAt: string;
  updatedAt: string;
}

export function FeishuPublishTab({
  workspaceId,
  channelId,
  locale,
  fetcher,
  webhookUrl,
  testId = "studio-publish-feishu-tab"
}: FeishuPublishTabProps) {
  const copy = getStudioCopy(locale);
  const baseUrl = `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/publish-channels/${encodeURIComponent(channelId)}/feishu-credential`;
  const fields: ReadonlyArray<ChannelCredentialField<FeishuCredentialDto>> = [
    {
      field: "appId",
      label: copy.feishuTab.appIdLabel,
      required: true,
      initValue: (credential) => credential?.appId ?? ""
    },
    {
      field: "appSecret",
      label: copy.feishuTab.formAppSecretLabel,
      type: "password",
      required: true
    },
    {
      field: "verificationToken",
      label: copy.feishuTab.verificationTokenLabel,
      required: true,
      initValue: (credential) => credential?.verificationToken ?? ""
    },
    {
      field: "encryptKey",
      label: copy.feishuTab.formEncryptKeyOptionalLabel,
      type: "password"
    }
  ];
  const summaries: ReadonlyArray<ChannelCredentialSummaryItem<FeishuCredentialDto>> = [
    {
      key: "appId",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.feishuTab.appIdLabel}: <strong>{credential.appId}</strong>
          <span style={{ marginLeft: 8 }}>({credential.appIdMasked})</span>
        </Typography.Text>
      )
    },
    {
      key: "verificationToken",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.feishuTab.verificationTokenLabel}: {credential.verificationToken}
        </Typography.Text>
      )
    },
    {
      key: "encryptKey",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.feishuTab.encryptKeyLabel}: {credential.hasEncryptKey ? copy.feishuTab.encryptKeyConfigured : copy.feishuTab.encryptKeyNotSet}
        </Typography.Text>
      )
    },
    {
      key: "refreshCount",
      render: (credential) => (
        <Typography.Text type="tertiary" size="small">
          {copy.feishuTab.refreshCountLabel}: {credential.refreshCount}
        </Typography.Text>
      )
    },
    {
      key: "expiresAt",
      render: (credential) =>
        credential.tenantAccessTokenExpiresAt ? (
          <Typography.Text type="tertiary" size="small">
            {copy.feishuTab.tokenExpiresAtLabel}: {credential.tenantAccessTokenExpiresAt}
          </Typography.Text>
        ) : null
    }
  ];

  return (
    <ChannelCredentialFormCard<FeishuCredentialDto>
      title={copy.feishuTab.title}
      hint={copy.feishuTab.hint}
      loadingText={copy.feishuTab.loading}
      saveSuccessText={copy.feishuTab.credentialSaved}
      clearSuccessText={copy.feishuTab.credentialCleared}
      saveButtonText={copy.feishuTab.saveCredential}
      clearButtonText={copy.feishuTab.clear}
      baseUrl={baseUrl}
      fetcher={fetcher}
      fields={fields}
      summaries={summaries}
      buildSubmitBody={(values) => ({
        appId: String(values.appId ?? "").trim(),
        appSecret: String(values.appSecret ?? ""),
        verificationToken: String(values.verificationToken ?? "").trim(),
        encryptKey: values.encryptKey ? String(values.encryptKey) : null
      })}
      webhookUrl={webhookUrl}
      webhookHint={copy.feishuTab.webhookHint}
      testId={testId}
    />
  );
}

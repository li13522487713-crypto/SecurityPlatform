import { useEffect, useState } from "react";
import { Banner, Button, Card, Form, Space, Spin, Toast, Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";
import { getStudioCopy } from "../copy";

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
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [credential, setCredential] = useState<FeishuCredentialDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const baseUrl = `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/publish-channels/${encodeURIComponent(channelId)}/feishu-credential`;

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const dto = await fetcher<FeishuCredentialDto | null>({ url: baseUrl, method: "GET" });
      setCredential(dto ?? null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "load failed");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [workspaceId, channelId]);

  async function handleSubmit(values: Record<string, unknown>) {
    setSubmitting(true);
    setError(null);
    try {
      const dto = await fetcher<FeishuCredentialDto>({
        url: baseUrl,
        method: "PUT",
        body: {
          appId: String(values.appId ?? "").trim(),
          appSecret: String(values.appSecret ?? ""),
          verificationToken: String(values.verificationToken ?? "").trim(),
          encryptKey: values.encryptKey ? String(values.encryptKey) : null
        }
      });
      setCredential(dto);
      Toast.success(copy.feishuTab.credentialSaved);
    } catch (e) {
      const msg = e instanceof Error ? e.message : "save failed";
      setError(msg);
      Toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete() {
    setSubmitting(true);
    setError(null);
    try {
      await fetcher({ url: baseUrl, method: "DELETE" });
      setCredential(null);
      Toast.success(copy.feishuTab.credentialCleared);
    } catch (e) {
      const msg = e instanceof Error ? e.message : "delete failed";
      setError(msg);
      Toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card data-testid={testId} title={copy.feishuTab.title} bordered>
      <Typography.Paragraph type="tertiary">{copy.feishuTab.hint}</Typography.Paragraph>

      {error ? (
        <Banner type="danger" description={error} closeIcon={null} fullMode={false} />
      ) : null}

      {loading ? (
        <Spin size="middle" tip={copy.feishuTab.loading} />
      ) : (
        <>
          {credential ? (
            <Space vertical align="start" spacing={6} style={{ marginBottom: 12, width: "100%" }}>
              <Typography.Text type="tertiary" size="small">
                {copy.feishuTab.appIdLabel}: <strong>{credential.appId}</strong>
                <span style={{ marginLeft: 8 }}>({credential.appIdMasked})</span>
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {copy.feishuTab.verificationTokenLabel}: {credential.verificationToken}
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {copy.feishuTab.encryptKeyLabel}: {credential.hasEncryptKey ? copy.feishuTab.encryptKeyConfigured : copy.feishuTab.encryptKeyNotSet}
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {copy.feishuTab.refreshCountLabel}: {credential.refreshCount}
              </Typography.Text>
              {credential.tenantAccessTokenExpiresAt ? (
                <Typography.Text type="tertiary" size="small">
                  {copy.feishuTab.tokenExpiresAtLabel}: {credential.tenantAccessTokenExpiresAt}
                </Typography.Text>
              ) : null}
            </Space>
          ) : null}

          {webhookUrl ? (
            <Banner
              type="info"
              fullMode={false}
              description={
                <Typography.Text size="small">
                  {copy.feishuTab.webhookHint}
                  <code>{webhookUrl}</code>
                </Typography.Text>
              }
              closeIcon={null}
            />
          ) : null}

          <Form layout="vertical" onSubmit={handleSubmit}>
            <Form.Input
              field="appId"
              label={copy.feishuTab.appIdLabel}
              initValue={credential?.appId ?? ""}
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="appSecret"
              label={copy.feishuTab.formAppSecretLabel}
              type="password"
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="verificationToken"
              label={copy.feishuTab.verificationTokenLabel}
              initValue={credential?.verificationToken ?? ""}
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="encryptKey"
              label={copy.feishuTab.formEncryptKeyOptionalLabel}
              type="password"
            />

            <Space>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {copy.feishuTab.saveCredential}
              </Button>
              {credential ? (
                <Button type="danger" loading={submitting} onClick={handleDelete}>
                  {copy.feishuTab.clear}
                </Button>
              ) : null}
            </Space>
          </Form>
        </>
      )}
    </Card>
  );
}

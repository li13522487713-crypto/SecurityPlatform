import { useEffect, useState } from "react";
import { Banner, Button, Card, Form, Space, Spin, Toast, Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";
import { getStudioCopy } from "../copy";

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
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [credential, setCredential] = useState<WechatMpCredentialDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const baseUrl = `/api/v1/workspaces/${encodeURIComponent(workspaceId)}/publish-channels/${encodeURIComponent(channelId)}/wechat-mp-credential`;

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const dto = await fetcher<WechatMpCredentialDto | null>({ url: baseUrl, method: "GET" });
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
      const dto = await fetcher<WechatMpCredentialDto>({
        url: baseUrl,
        method: "PUT",
        body: {
          appId: String(values.appId ?? "").trim(),
          appSecret: String(values.appSecret ?? ""),
          token: String(values.token ?? "").trim(),
          encodingAesKey: values.encodingAesKey ? String(values.encodingAesKey) : null
        }
      });
      setCredential(dto);
      Toast.success(copy.wechatMpTab.credentialSaved);
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
      Toast.success(copy.wechatMpTab.credentialCleared);
    } catch (e) {
      const msg = e instanceof Error ? e.message : "delete failed";
      setError(msg);
      Toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card data-testid={testId} title={copy.wechatMpTab.title} bordered>
      <Typography.Paragraph type="tertiary">{copy.wechatMpTab.hint}</Typography.Paragraph>
      {error ? <Banner type="danger" description={error} closeIcon={null} fullMode={false} /> : null}
      {loading ? (
        <Spin size="middle" tip={copy.wechatMpTab.loading} />
      ) : (
        <>
          {credential ? (
            <Space vertical align="start" spacing={6} style={{ marginBottom: 12, width: "100%" }}>
              <Typography.Text type="tertiary" size="small">
                {copy.wechatMpTab.appIdLabel}: <strong>{credential.appId}</strong>
                <span style={{ marginLeft: 8 }}>({credential.appIdMasked})</span>
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {copy.wechatMpTab.serverTokenLabel}: {credential.token}
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {copy.wechatMpTab.encodingAesKeyLabel}: {credential.hasEncodingAesKey ? copy.wechatMpTab.encodingAesKeyConfigured : copy.wechatMpTab.encodingAesKeyNotSet}
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {copy.wechatMpTab.accessTokenRefreshCountLabel}: {credential.refreshCount}
              </Typography.Text>
              {credential.accessTokenExpiresAt ? (
                <Typography.Text type="tertiary" size="small">
                  {copy.wechatMpTab.accessTokenExpiresAtLabel}: {credential.accessTokenExpiresAt}
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
                  {copy.wechatMpTab.webhookHint}
                  <code>{webhookUrl}</code>
                </Typography.Text>
              }
              closeIcon={null}
            />
          ) : null}

          <Form layout="vertical" onSubmit={handleSubmit}>
            <Form.Input
              field="appId"
              label={copy.wechatMpTab.appIdLabel}
              initValue={credential?.appId ?? ""}
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="appSecret"
              label={copy.wechatMpTab.formAppSecretLabel}
              type="password"
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="token"
              label={copy.wechatMpTab.serverTokenLabel}
              initValue={credential?.token ?? ""}
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="encodingAesKey"
              label={copy.wechatMpTab.formEncodingAesKeyOptionalLabel}
              type="password"
            />
            <Space>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {copy.wechatMpTab.saveCredential}
              </Button>
              {credential ? (
                <Button type="danger" loading={submitting} onClick={handleDelete}>
                  {copy.wechatMpTab.clear}
                </Button>
              ) : null}
            </Space>
          </Form>
        </>
      )}
    </Card>
  );
}

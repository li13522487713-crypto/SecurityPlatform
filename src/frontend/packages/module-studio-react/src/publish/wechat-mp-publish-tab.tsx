import { useEffect, useState } from "react";
import { Banner, Button, Card, Form, Space, Spin, Toast, Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";

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
      Toast.success(locale === "en-US" ? "WeChat MP credential saved." : "微信公众号凭据已保存。");
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
      Toast.success(locale === "en-US" ? "WeChat MP credential cleared." : "微信公众号凭据已清除。");
    } catch (e) {
      const msg = e instanceof Error ? e.message : "delete failed";
      setError(msg);
      Toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  const title = locale === "en-US" ? "WeChat Official Account" : "微信公众号渠道";
  const hint =
    locale === "en-US"
      ? "Provide AppId / AppSecret / Token / EncodingAesKey from the WeChat Official Account admin console."
      : "在微信公众平台「基本配置」中获取 AppId、AppSecret、Token 与 EncodingAesKey 后填写。";

  return (
    <Card data-testid={testId} title={title} bordered>
      <Typography.Paragraph type="tertiary">{hint}</Typography.Paragraph>
      {error ? <Banner type="danger" description={error} closeIcon={null} fullMode={false} /> : null}
      {loading ? (
        <Spin size="middle" tip={locale === "en-US" ? "Loading…" : "加载中…"} />
      ) : (
        <>
          {credential ? (
            <Space vertical align="start" spacing={6} style={{ marginBottom: 12, width: "100%" }}>
              <Typography.Text type="tertiary" size="small">
                {locale === "en-US" ? "App Id" : "App Id"}：<strong>{credential.appId}</strong>
                <span style={{ marginLeft: 8 }}>({credential.appIdMasked})</span>
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {locale === "en-US" ? "Server Token" : "服务器 Token"}：{credential.token}
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {locale === "en-US" ? "EncodingAesKey" : "EncodingAesKey"}：
                {credential.hasEncodingAesKey ? (locale === "en-US" ? "configured" : "已配置") : locale === "en-US" ? "not set" : "未设置"}
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {locale === "en-US" ? "Access token refresh count" : "AccessToken 刷新次数"}：{credential.refreshCount}
              </Typography.Text>
              {credential.accessTokenExpiresAt ? (
                <Typography.Text type="tertiary" size="small">
                  {locale === "en-US" ? "Access token expires at" : "AccessToken 过期时间"}：{credential.accessTokenExpiresAt}
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
                  {locale === "en-US" ? "Configure WeChat MP server URL: " : "请在微信公众平台「服务器地址 (URL)」中填写："}
                  <code>{webhookUrl}</code>
                </Typography.Text>
              }
              closeIcon={null}
            />
          ) : null}

          <Form layout="vertical" onSubmit={handleSubmit}>
            <Form.Input
              field="appId"
              label={locale === "en-US" ? "App Id" : "App Id"}
              initValue={credential?.appId ?? ""}
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="appSecret"
              label={locale === "en-US" ? "App Secret" : "App Secret（落库前自动加密）"}
              type="password"
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="token"
              label={locale === "en-US" ? "Server Token" : "服务器 Token"}
              initValue={credential?.token ?? ""}
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="encodingAesKey"
              label={locale === "en-US" ? "EncodingAesKey (optional)" : "EncodingAesKey（可选）"}
              type="password"
            />
            <Space>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {locale === "en-US" ? "Save credential" : "保存凭据"}
              </Button>
              {credential ? (
                <Button type="danger" loading={submitting} onClick={handleDelete}>
                  {locale === "en-US" ? "Clear" : "清除"}
                </Button>
              ) : null}
            </Space>
          </Form>
        </>
      )}
    </Card>
  );
}

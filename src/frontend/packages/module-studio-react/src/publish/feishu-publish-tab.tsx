import { useEffect, useState } from "react";
import { Banner, Button, Card, Form, Input, Space, Spin, Toast, Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";

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
      Toast.success(locale === "en-US" ? "Feishu credential saved." : "飞书凭据已保存。");
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
      Toast.success(locale === "en-US" ? "Feishu credential cleared." : "飞书凭据已清除。");
    } catch (e) {
      const msg = e instanceof Error ? e.message : "delete failed";
      setError(msg);
      Toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  const title = locale === "en-US" ? "Feishu (Lark) channel" : "飞书渠道";
  const hint =
    locale === "en-US"
      ? "Provide app id, app secret and event verification token from the Feishu Open Platform. Secrets are encrypted at rest."
      : "在飞书开放平台获取应用凭据后填写；AppSecret / EncryptKey 在落库前会用平台密钥加密。";

  return (
    <Card data-testid={testId} title={title} bordered>
      <Typography.Paragraph type="tertiary">{hint}</Typography.Paragraph>

      {error ? (
        <Banner type="danger" description={error} closeIcon={null} fullMode={false} />
      ) : null}

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
                {locale === "en-US" ? "Verification Token" : "校验 Token"}：{credential.verificationToken}
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {locale === "en-US" ? "Encrypt Key" : "Encrypt Key"}：
                {credential.hasEncryptKey ? (locale === "en-US" ? "configured" : "已配置") : locale === "en-US" ? "not set" : "未设置"}
              </Typography.Text>
              <Typography.Text type="tertiary" size="small">
                {locale === "en-US" ? "Last token refresh count" : "Token 刷新次数"}：{credential.refreshCount}
              </Typography.Text>
              {credential.tenantAccessTokenExpiresAt ? (
                <Typography.Text type="tertiary" size="small">
                  {locale === "en-US" ? "Token expires at" : "Token 过期时间"}：{credential.tenantAccessTokenExpiresAt}
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
                  {locale === "en-US" ? "Configure Feishu event subscription URL: " : "请在飞书事件订阅中填写："}
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
              label={locale === "en-US" ? "App Secret (will be encrypted)" : "App Secret（落库前自动加密）"}
              type="password"
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="verificationToken"
              label={locale === "en-US" ? "Verification Token" : "校验 Token"}
              initValue={credential?.verificationToken ?? ""}
              rules={[{ required: true, message: "required" }]}
            />
            <Form.Input
              field="encryptKey"
              label={locale === "en-US" ? "Encrypt Key (optional)" : "Encrypt Key（可选）"}
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

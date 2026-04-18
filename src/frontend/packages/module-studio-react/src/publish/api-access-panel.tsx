import { Card, Tag, Typography } from "@douyinfe/semi-ui";
import type { OpenApiPublicMeta, StudioLocale } from "../types";

export interface ApiAccessPanelProps {
  locale: StudioLocale;
  /** 示例 API 根路径，例如 https://host/api/v1 */
  apiBase?: string;
  /** 示例资源路径片段，例如 agents/{id}/chat */
  resourcePath?: string;
  /** 示例 Bearer Token（脱敏展示） */
  sampleBearerToken?: string;
  /**
   * 治理 R1-F2：当前 Open API 渠道 active release 的 publicMetadataJson 解析结果。
   * 提供时渲染真实 endpoint / tokenMasked / 调用示例 / rate limit；
   * 未提供时保留旧静态模板（向后兼容）。
   */
  openApiPublicMeta?: OpenApiPublicMeta;
  testId?: string;
}

export function ApiAccessPanel({
  locale,
  apiBase = `${typeof window !== "undefined" ? window.location.origin : ""}/api/v1`,
  resourcePath = "studio/publish-center/example",
  sampleBearerToken = "<your_access_token>",
  openApiPublicMeta,
  testId = "studio-publish-api-access-panel"
}: ApiAccessPanelProps) {
  const effectiveEndpoint = openApiPublicMeta?.endpoint
    ? openApiPublicMeta.endpoint
    : `${apiBase.replace(/\/$/, "")}/${resourcePath.replace(/^\//, "")}`;
  const effectiveToken = openApiPublicMeta?.tokenMasked || sampleBearerToken;

  const curl = [
    `curl -sS -X POST \\`,
    `  "${effectiveEndpoint}" \\`,
    `  -H "Authorization: Bearer ${effectiveToken}" \\`,
    `  -H "X-Tenant-Id: <tenant_guid>" \\`,
    `  -H "Content-Type: application/json" \\`,
    `  -d '{"agentId":"<published_agent_id>","message":"hello"}'`
  ].join("\n");

  const title = locale === "en-US" ? "HTTP / API access" : "HTTP / API 接入";
  const hint =
    locale === "en-US"
      ? "Replace placeholders with your tenant, token, and the endpoint returned for each published resource."
      : "将占位符替换为租户 ID、访问令牌，以及各已发布资源返回的实际 API 路径。";

  return (
    <Card data-testid={testId} title={title} bordered>
      <Typography.Paragraph type="tertiary">{hint}</Typography.Paragraph>

      {openApiPublicMeta ? (
        <>
          <Typography.Title heading={6}>
            {locale === "en-US" ? "Active endpoint" : "当前发布端点"}
          </Typography.Title>
          <Typography.Paragraph>
            <Typography.Text copyable>{effectiveEndpoint}</Typography.Text>
          </Typography.Paragraph>
          <Typography.Paragraph>
            {locale === "en-US" ? "Token (masked): " : "令牌（脱敏）："}
            <Typography.Text>{openApiPublicMeta.tokenMasked || "—"}</Typography.Text>
          </Typography.Paragraph>
          <Typography.Paragraph>
            {locale === "en-US" ? "Rate limit: " : "速率限制："}
            <Tag color="blue">{openApiPublicMeta.rateLimitPerMinute}/min</Tag>
          </Typography.Paragraph>
          {openApiPublicMeta.endpoints.length > 0 ? (
            <>
              <Typography.Title heading={6} style={{ marginTop: 12 }}>
                {locale === "en-US" ? "Available endpoints" : "可用端点"}
              </Typography.Title>
              <ul style={{ marginTop: 4, paddingLeft: 18 }}>
                {openApiPublicMeta.endpoints.map((ep) => (
                  <li key={ep}>
                    <Typography.Text code>{ep}</Typography.Text>
                  </li>
                ))}
              </ul>
            </>
          ) : null}
        </>
      ) : null}

      <Typography.Title heading={6} style={{ marginTop: openApiPublicMeta ? 24 : 0 }}>
        {locale === "en-US" ? "cURL" : "cURL 示例"}
      </Typography.Title>
      <pre className="module-studio__message-content" style={{ marginTop: 8 }} data-testid={`${testId}-curl`}>
        {curl}
      </pre>
      <Typography.Title heading={6} style={{ marginTop: 16 }}>
        {locale === "en-US" ? "Headers" : "请求头说明"}
      </Typography.Title>
      <Typography.Text type="tertiary">
        {locale === "en-US"
          ? "Authorization: JWT from sign-in. X-Tenant-Id: must match the tenant bound to the token."
          : "Authorization：登录后下发的 JWT。X-Tenant-Id：必须与令牌中的租户一致。"}
      </Typography.Text>
    </Card>
  );
}

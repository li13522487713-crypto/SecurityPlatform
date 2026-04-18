import { Card, Tag, Typography } from "@douyinfe/semi-ui";
import type { OpenApiPublicMeta, StudioLocale } from "../types";
import { getStudioCopy } from "../copy";

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
  const copy = getStudioCopy(locale);
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

  return (
    <Card data-testid={testId} title={copy.apiAccess.title} bordered>
      <Typography.Paragraph type="tertiary">{copy.apiAccess.hint}</Typography.Paragraph>

      {openApiPublicMeta ? (
        <>
          <Typography.Title heading={6}>{copy.apiAccess.activeEndpoint}</Typography.Title>
          <Typography.Paragraph>
            <Typography.Text copyable>{effectiveEndpoint}</Typography.Text>
          </Typography.Paragraph>
          <Typography.Paragraph>
            {copy.apiAccess.tokenLabel}
            <Typography.Text>{openApiPublicMeta.tokenMasked || "—"}</Typography.Text>
          </Typography.Paragraph>
          <Typography.Paragraph>
            {copy.apiAccess.rateLimitLabel}
            <Tag color="blue">{openApiPublicMeta.rateLimitPerMinute}/min</Tag>
          </Typography.Paragraph>
          {openApiPublicMeta.endpoints.length > 0 ? (
            <>
              <Typography.Title heading={6} style={{ marginTop: 12 }}>
                {copy.apiAccess.availableEndpoints}
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
        {copy.apiAccess.curlSection}
      </Typography.Title>
      <pre className="module-studio__message-content" style={{ marginTop: 8 }} data-testid={`${testId}-curl`}>
        {curl}
      </pre>
      <Typography.Title heading={6} style={{ marginTop: 16 }}>
        {copy.apiAccess.headersSection}
      </Typography.Title>
      <Typography.Text type="tertiary">{copy.apiAccess.headersHint}</Typography.Text>
    </Card>
  );
}

import { Card, Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";

export interface ApiAccessPanelProps {
  locale: StudioLocale;
  /** 示例 API 根路径，例如 https://host/api/v1 */
  apiBase?: string;
  /** 示例资源路径片段，例如 agents/{id}/chat */
  resourcePath?: string;
  /** 示例 Bearer Token（脱敏展示） */
  sampleBearerToken?: string;
  testId?: string;
}

export function ApiAccessPanel({
  locale,
  apiBase = `${typeof window !== "undefined" ? window.location.origin : ""}/api/v1`,
  resourcePath = "studio/publish-center/example",
  sampleBearerToken = "<your_access_token>",
  testId = "studio-publish-api-access-panel"
}: ApiAccessPanelProps) {
  const curl = [
    `curl -sS -X GET \\`,
    `  "${apiBase.replace(/\/$/, "")}/${resourcePath.replace(/^\//, "")}" \\`,
    `  -H "Authorization: Bearer ${sampleBearerToken}" \\`,
    `  -H "X-Tenant-Id: <tenant_guid>" \\`,
    `  -H "Accept: application/json"`
  ].join("\n");

  const title = locale === "en-US" ? "HTTP / API access" : "HTTP / API 接入";
  const hint =
    locale === "en-US"
      ? "Replace placeholders with your tenant, token, and the endpoint returned for each published resource."
      : "将占位符替换为租户 ID、访问令牌，以及各已发布资源返回的实际 API 路径。";

  return (
    <Card data-testid={testId} title={title} bordered>
      <Typography.Paragraph type="tertiary">{hint}</Typography.Paragraph>
      <Typography.Title heading={6}>{locale === "en-US" ? "cURL" : "cURL 示例"}</Typography.Title>
      <pre className="module-studio__message-content" style={{ marginTop: 8 }}>
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

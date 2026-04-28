import { Card, Tag, Typography } from "@douyinfe/semi-ui";
import type { StudioLocale, WebSdkPublicMeta } from "../types";
import { getStudioCopy } from "../copy";

export interface ChatSdkPanelProps {
  locale: StudioLocale;
  /** 示例聊天接口，占位 */
  chatEndpoint?: string;
  /**
   * 治理 R1-F2：当前 Web SDK 渠道 active release 的 publicMetadataJson 解析结果。
   * 提供时渲染真实 snippet / endpoint / secretMasked / origin allowlist；
   * 未提供时保留旧静态模板（向后兼容）。
   */
  webSdkPublicMeta?: WebSdkPublicMeta;
  testId?: string;
}

export function ChatSdkPanel({
  locale,
  chatEndpoint = `${typeof window !== "undefined" ? window.location.origin : ""}/api/v1/ai/chat`,
  webSdkPublicMeta,
  testId = "studio-publish-chat-sdk-panel"
}: ChatSdkPanelProps) {
  const copy = getStudioCopy(locale);

  /* 代码示例使用 ASCII / 通用编程惯例文本，hello 在中文上下文下显示 "Hi" 即可避免硬编码 CJK；
     代码注释统一英文，与平台开发者文档一致。 */
  const reactSnippet = [
    `// React: invoke a published agent via fetch from inside the host app`,
    `const res = await fetch("${chatEndpoint}", {`,
    `  method: "POST",`,
    `  headers: {`,
    `    "Content-Type": "application/json",`,
    `    "Authorization": \`Bearer \${accessToken}\`,`,
    `    "X-Tenant-Id": tenantId`,
    `  },`,
    `  body: JSON.stringify({`,
    `    agentId: "<published_agent_id>",`,
    `    conversationId: undefined,`,
    `    message: "Hello",`,
    `    enableRag: true`,
    `  })`,
    `});`
  ].join("\n");

  const webSnippet = [
    `// Browser Web (handle CORS and store tokens securely)`,
    `await fetch("${chatEndpoint}", {`,
    `  method: "POST",`,
    `  credentials: "include",`,
    `  headers: { "Content-Type": "application/json" },`,
    `  body: JSON.stringify({ agentId: "<published_agent_id>", message: "Hi" })`,
    `});`
  ].join("\n");

  const title = copy.chatSdk.title;
  const hint = copy.chatSdk.hint;

  return (
    <Card data-testid={testId} title={title} bordered>
      <Typography.Paragraph type="tertiary">{hint}</Typography.Paragraph>

      {webSdkPublicMeta ? (
        <>
          <Typography.Title heading={6}>{copy.chatSdk.activeSnippet}</Typography.Title>
          {webSdkPublicMeta.snippet ? (
            <pre className="module-studio__message-content" style={{ marginTop: 8 }} data-testid={`${testId}-snippet`}>
              {webSdkPublicMeta.snippet}
            </pre>
          ) : (
            <Typography.Text type="tertiary">{copy.chatSdk.snippetMissing}</Typography.Text>
          )}
          <Typography.Paragraph style={{ marginTop: 8 }}>
            {copy.chatSdk.endpointLabel}
            <Typography.Text copyable={Boolean(webSdkPublicMeta.endpoint)}>{webSdkPublicMeta.endpoint || "—"}</Typography.Text>
          </Typography.Paragraph>
          <Typography.Paragraph>
            {copy.chatSdk.secretLabel}
            <Typography.Text>{webSdkPublicMeta.secretMasked || "—"}</Typography.Text>
          </Typography.Paragraph>
          <Typography.Paragraph>
            {copy.chatSdk.originsLabel}
            {webSdkPublicMeta.originAllowlist.length === 0 ? (
              <Tag color="amber">{copy.chatSdk.originsNoRestriction}</Tag>
            ) : (
              webSdkPublicMeta.originAllowlist.map((origin) => (
                <Tag key={origin} style={{ marginRight: 4 }}>
                  {origin}
                </Tag>
              ))
            )}
          </Typography.Paragraph>
        </>
      ) : null}

      <Typography.Title heading={6} style={{ marginTop: webSdkPublicMeta ? 24 : 0 }}>
        React
      </Typography.Title>
      <pre className="module-studio__message-content" style={{ marginTop: 8 }}>
        {reactSnippet}
      </pre>
      <Typography.Title heading={6} style={{ marginTop: 16 }}>
        Web
      </Typography.Title>
      <pre className="module-studio__message-content" style={{ marginTop: 8 }}>
        {webSnippet}
      </pre>
    </Card>
  );
}

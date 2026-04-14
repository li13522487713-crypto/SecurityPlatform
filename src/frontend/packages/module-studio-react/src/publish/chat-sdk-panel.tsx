import { Card, Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";

export interface ChatSdkPanelProps {
  locale: StudioLocale;
  /** 示例聊天接口，占位 */
  chatEndpoint?: string;
  testId?: string;
}

export function ChatSdkPanel({
  locale,
  chatEndpoint = `${typeof window !== "undefined" ? window.location.origin : ""}/api/v1/ai/chat`,
  testId = "studio-publish-chat-sdk-panel"
}: ChatSdkPanelProps) {
  const reactSnippet = [
    `// React（示例：在宿主内使用 fetch 调用已发布智能体）`,
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
    `    message: "你好",`,
    `    enableRag: true`,
    `  })`,
    `});`
  ].join("\n");

  const webSnippet = [
    `// 浏览器原生 Web（需处理 CORS 与令牌安全存储）`,
    `await fetch("${chatEndpoint}", {`,
    `  method: "POST",`,
    `  credentials: "include",`,
    `  headers: { "Content-Type": "application/json" },`,
    `  body: JSON.stringify({ agentId: "<published_agent_id>", message: "Hi" })`,
    `});`
  ].join("\n");

  const title = locale === "en-US" ? "Web / React snippets" : "Web / React 示例";
  const hint =
    locale === "en-US"
      ? "Use the published agent id and the API base from the release center. Prefer server-side proxy for tokens in production."
      : "使用发布中心展示的已发布智能体 ID 与 API 根路径；生产环境建议通过服务端代理保护令牌。";

  return (
    <Card data-testid={testId} title={title} bordered>
      <Typography.Paragraph type="tertiary">{hint}</Typography.Paragraph>
      <Typography.Title heading={6}>React</Typography.Title>
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

import { useEffect, useMemo, useState } from "react";
import { Banner, Card, Spin, Toast, Typography } from "@douyinfe/semi-ui";
import type {
  OpenApiPublicMeta,
  PublishChannelActiveRelease,
  PublishChannelListItem,
  StudioLocale,
  WebSdkPublicMeta
} from "../types";
import { ApiAccessPanel } from "./api-access-panel";
import { ChatSdkPanel } from "./chat-sdk-panel";
import { FeishuPublishTab } from "./feishu-publish-tab";
import { WechatCsPublishTab } from "./wechat-cs-publish-tab";
import { WechatMiniappPublishTab } from "./wechat-miniapp-publish-tab";
import { WechatMpPublishTab } from "./wechat-mp-publish-tab";
import { formatStudioTemplate, getStudioCopy } from "../copy";

/**
 * 治理 R1-F1：根据 channel.type 调度具体配置面板。
 *
 * 数据流：
 * 1. 拉取 active release（可能不存在）→ 解析 publicMetadataJson；
 * 2. web-sdk → ChatSdkPanel(webSdkPublicMeta)；
 * 3. open-api → ApiAccessPanel(openApiPublicMeta)；
 * 4. feishu / wechat-mp → 凭据 Tab，展示 webhookUrl；
 * 5. 其他类型（custom / lark / wechat 企业微信）→ ApiAccessPanel 默认模板兜底。
 */
export interface ChannelDetailRouterProps {
  workspaceId: string;
  locale: StudioLocale;
  channel: PublishChannelListItem;
  releaseLoader: (workspaceId: string, channelId: string) => Promise<PublishChannelActiveRelease | null>;
  fetcher: <T = unknown>(input: { url: string; method: string; body?: unknown }) => Promise<T>;
  apiBase?: string;
  testId?: string;
}

interface ParsedRelease {
  raw: PublishChannelActiveRelease | null;
  webSdk?: WebSdkPublicMeta;
  openApi?: OpenApiPublicMeta;
  webhookUrl?: string;
}

function parseRelease(release: PublishChannelActiveRelease | null, type: string): ParsedRelease {
  if (release === null || !release.publicMetadataJson) {
    return { raw: release };
  }
  let parsed: Record<string, unknown> | null = null;
  try {
    parsed = JSON.parse(release.publicMetadataJson) as Record<string, unknown>;
  } catch {
    parsed = null;
  }
  if (parsed === null) {
    return { raw: release };
  }
  const lower = type.toLowerCase();
  if (lower === "web-sdk") {
    const meta: WebSdkPublicMeta = {
      snippet: typeof parsed.snippet === "string" ? parsed.snippet : "",
      endpoint: typeof parsed.endpoint === "string" ? parsed.endpoint : "",
      secretMasked: typeof parsed.secretMasked === "string" ? parsed.secretMasked : "",
      originAllowlist: Array.isArray(parsed.originAllowlist)
        ? (parsed.originAllowlist.filter((x): x is string => typeof x === "string") as string[])
        : []
    };
    return { raw: release, webSdk: meta };
  }
  if (lower === "open-api") {
    const meta: OpenApiPublicMeta = {
      endpoint: typeof parsed.endpoint === "string" ? parsed.endpoint : "",
      tokenMasked: typeof parsed.tokenMasked === "string" ? parsed.tokenMasked : "",
      endpoints: Array.isArray(parsed.endpoints)
        ? (parsed.endpoints.filter((x): x is string => typeof x === "string") as string[])
        : [],
      rateLimitPerMinute: typeof parsed.rateLimitPerMinute === "number" ? parsed.rateLimitPerMinute : 60
    };
    return { raw: release, openApi: meta };
  }
  if (lower === "feishu" || lower === "wechat-mp" || lower === "wechat-miniapp" || lower === "wechat-cs" || lower === "lark") {
    const webhookUrl = typeof parsed.webhookUrl === "string" ? parsed.webhookUrl : undefined;
    return { raw: release, webhookUrl };
  }
  return { raw: release };
}

export function ChannelDetailRouter({
  workspaceId,
  locale,
  channel,
  releaseLoader,
  fetcher,
  apiBase,
  testId = "studio-publish-channel-detail"
}: ChannelDetailRouterProps) {
  const [loading, setLoading] = useState(true);
  const [parsed, setParsed] = useState<ParsedRelease>({ raw: null });

  useEffect(() => {
    let disposed = false;
    setLoading(true);
    void releaseLoader(workspaceId, channel.id)
      .then((release) => {
        if (disposed) return;
        setParsed(parseRelease(release, channel.type));
      })
      .catch((e: unknown) => {
        if (!disposed) {
          Toast.error(
            e instanceof Error ? e.message : getStudioCopy(locale).channelDetail.loadActiveReleaseFailed
          );
          setParsed({ raw: null });
        }
      })
      .finally(() => {
        if (!disposed) setLoading(false);
      });
    return () => {
      disposed = true;
    };
  }, [releaseLoader, workspaceId, channel.id, channel.type, locale]);

  const headerNote = useMemo(() => {
    const copy = getStudioCopy(locale);
    if (parsed.raw === null) {
      return copy.channelDetail.noActiveRelease;
    }
    return formatStudioTemplate(copy.channelDetail.activeReleaseInfoTemplate, {
      releaseNo: parsed.raw.releaseNo,
      status: parsed.raw.status
    });
  }, [parsed.raw, locale]);

  if (loading) {
    return <Spin />;
  }

  const lower = channel.type.toLowerCase();
  return (
    <div data-testid={testId}>
      <Banner type={parsed.raw ? "info" : "warning"} description={headerNote} closeIcon={null} style={{ marginBottom: 12 }} />
      {lower === "web-sdk" ? (
        <ChatSdkPanel locale={locale} webSdkPublicMeta={parsed.webSdk} />
      ) : lower === "open-api" ? (
        <ApiAccessPanel
          locale={locale}
          apiBase={apiBase}
          resourcePath="agents/{id}/runtime"
          sampleBearerToken="<access_token>"
          openApiPublicMeta={parsed.openApi}
        />
      ) : lower === "feishu" ? (
        <FeishuPublishTab
          workspaceId={workspaceId}
          channelId={channel.id}
          locale={locale}
          fetcher={fetcher}
          webhookUrl={parsed.webhookUrl}
        />
      ) : lower === "wechat-mp" ? (
        <WechatMpPublishTab
          workspaceId={workspaceId}
          channelId={channel.id}
          locale={locale}
          fetcher={fetcher}
          webhookUrl={parsed.webhookUrl}
        />
      ) : lower === "wechat-miniapp" ? (
        <WechatMiniappPublishTab
          workspaceId={workspaceId}
          channelId={channel.id}
          locale={locale}
          fetcher={fetcher}
          webhookUrl={parsed.webhookUrl}
        />
      ) : lower === "wechat-cs" ? (
        <WechatCsPublishTab
          workspaceId={workspaceId}
          channelId={channel.id}
          locale={locale}
          fetcher={fetcher}
          webhookUrl={parsed.webhookUrl}
        />
      ) : (
        <Card>
          <Typography.Title heading={5}>
            {getStudioCopy(locale).channelDetail.channelUiNotAvailable}
          </Typography.Title>
          <Typography.Paragraph type="tertiary">
            {formatStudioTemplate(
              getStudioCopy(locale).channelDetail.channelUiNotAvailableHintTemplate,
              { type: channel.type }
            )}
          </Typography.Paragraph>
        </Card>
      )}
    </div>
  );
}

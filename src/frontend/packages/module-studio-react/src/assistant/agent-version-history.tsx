import { Tag, Typography } from "@douyinfe/semi-ui";
import type { StudioAssistantPublication } from "../types";
import { formatDate } from "./agent-ide-helpers";

export interface AgentVersionHistoryProps {
  publications: StudioAssistantPublication[];
  publicationLoading: boolean;
  /** 列表最多展示条数，默认 3（与旧版 Bot IDE 侧栏一致） */
  maxItems?: number;
}

export function AgentVersionHistory({
  publications,
  publicationLoading,
  maxItems = 3
}: AgentVersionHistoryProps) {
  if (publicationLoading) {
    return <Typography.Text type="tertiary">正在加载发布记录…</Typography.Text>;
  }

  if (publications.length === 0) {
    return <Typography.Text type="tertiary">当前还没有发布记录。</Typography.Text>;
  }

  return (
    <div className="module-studio__stack">
      {publications.slice(0, maxItems).map(item => (
        <div key={item.id} className="module-studio__coze-linkrow">
          <div>
            <strong>v{item.version}</strong>
            <div className="module-studio__meta">
              {item.isActive ? "当前激活" : "历史版本"} / {formatDate(item.createdAt)}
            </div>
            <div className="module-studio__meta">
              Token: {item.embedToken ? `${item.embedToken.slice(0, 12)}...` : "-"}
            </div>
          </div>
          <Tag color={item.isActive ? "green" : "grey"}>
            {item.isActive ? "Active" : "Archived"}
          </Tag>
        </div>
      ))}
    </div>
  );
}

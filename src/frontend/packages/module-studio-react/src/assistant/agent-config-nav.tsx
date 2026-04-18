import { Typography } from "@douyinfe/semi-ui";
import type { AgentConfigNavKey } from "./agent-workbench-types";
import { AGENT_CONFIG_SECTIONS } from "./agent-config-sections";
import { getStudioCopy } from "../copy";
import type { StudioLocale } from "../types";

export interface AgentConfigNavProps {
  activeKey: AgentConfigNavKey;
  onActiveKeyChange: (key: AgentConfigNavKey) => void;
  /** 顶部状态角标，例如草稿 / 已发布 */
  statusLabel?: string;
  /** 资源同步提示（模型数、工作流数等） */
  resourceHint?: string;
  workbenchLoading: boolean;
  locale: StudioLocale;
}

export function AgentConfigNav({
  activeKey,
  onActiveKeyChange,
  statusLabel,
  resourceHint,
  workbenchLoading,
  locale
}: AgentConfigNavProps) {
  const copy = getStudioCopy(locale);
  return (
    <nav className="module-studio__agent-workbench-nav" aria-label="Agent configuration">
      <div className="module-studio__section-head">
        <div>
          <Typography.Title heading={5} style={{ margin: 0 }}>{copy.assistant.configNavTitle}</Typography.Title>
          <Typography.Text type="tertiary">{copy.assistant.configNavSubtitle}</Typography.Text>
        </div>
        {statusLabel ? <span className="module-studio__meta">{statusLabel}</span> : null}
      </div>
      <div className="module-studio__coze-menu" role="tablist">
        {AGENT_CONFIG_SECTIONS.map(item => (
          <button
            key={item.key}
            type="button"
            role="tab"
            aria-selected={activeKey === item.key}
            className="module-studio__coze-menu-item"
            data-active={activeKey === item.key ? "true" : "false"}
            onClick={() => onActiveKeyChange(item.key)}
          >
            <div className="module-studio__stack" style={{ alignItems: "flex-start", textAlign: "left" }}>
              <strong>{item.label}</strong>
              {item.description ? (
                <Typography.Text type="tertiary" style={{ fontSize: 12 }}>{item.description}</Typography.Text>
              ) : null}
            </div>
          </button>
        ))}
      </div>
      <div className="module-studio__meta" data-testid="app-bot-ide-resource-status">
        {workbenchLoading ? copy.assistant.configNavLoadingResources : resourceHint}
      </div>
    </nav>
  );
}

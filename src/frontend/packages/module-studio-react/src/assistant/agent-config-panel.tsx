import type { ReactNode } from "react";
import { Tabs, Typography } from "@douyinfe/semi-ui";
import type { AgentConfigNavKey } from "./agent-workbench-types";
import { AGENT_CONFIG_SECTION_MAP, AGENT_CONFIG_SECTIONS } from "./agent-config-sections";

export interface AgentConfigPanelProps {
  /** 当前左侧导航选中的配置分区，与 `AgentConfigNav` 一致。 */
  activeNavKey: AgentConfigNavKey;
  /** 配置区 Tabs 切换时回写左侧导航状态，形成双向同步。 */
  onActiveNavKeyChange: (key: AgentConfigNavKey) => void;
  /** 当前 Tab 下的表单主体。 */
  children: ReactNode;
  /** 底部固定操作区（如保存），各 Tab 切换时仍可见。 */
  footer?: ReactNode;
}

/**
 * 中间配置区：使用 Semi Tabs 呈现并与左侧导航双向同步。
 */
export function AgentConfigPanel({
  activeNavKey,
  onActiveNavKeyChange,
  children,
  footer
}: AgentConfigPanelProps) {
  const head = AGENT_CONFIG_SECTION_MAP[activeNavKey];
  return (
    <div className="module-studio__agent-config-panel" data-active-nav={activeNavKey}>
      <header className="module-studio__agent-config-panel-header">
        <div className="module-studio__section-head module-studio__agent-config-panel-title-row">
          <div>
            <Typography.Title heading={5} style={{ margin: 0 }}>
              {head.title}
            </Typography.Title>
            <Typography.Text type="tertiary">{head.subtitle}</Typography.Text>
          </div>
        </div>
      </header>
      <Tabs
        type="line"
        keepDOM={false}
        activeKey={activeNavKey}
        onChange={itemKey => {
          const nextNav = AGENT_CONFIG_SECTIONS.find(section => section.key === itemKey);
          if (nextNav && nextNav.key !== activeNavKey) {
            onActiveNavKeyChange(nextNav.key);
          }
        }}
        className="module-studio__agent-config-tabs"
      >
        {AGENT_CONFIG_SECTIONS.map(section => (
          <Tabs.TabPane key={section.key} tab={section.label} itemKey={section.key}>
            {activeNavKey === section.key ? (
              <div className="module-studio__agent-config-panel-body module-studio__agent-config-tab-body">
                {children}
              </div>
            ) : null}
          </Tabs.TabPane>
        ))}
      </Tabs>
      {footer ? <footer className="module-studio__agent-config-panel-footer">{footer}</footer> : null}
    </div>
  );
}

import type { ReactNode } from "react";

export interface AgentWorkbenchLayoutProps {
  /** 左侧：配置导航 */
  nav: ReactNode;
  /** 中间：表单 / Tab 内容 */
  config: ReactNode;
  /** 右侧：调试、会话、Trace */
  debug: ReactNode;
}

/**
 * Coze 风格三列工作台：导航 | 配置 | 调试。
 * 样式见 `styles.css` 中 `.module-studio__agent-workbench-shell` / `.module-studio__agent-workbench-layout`。
 */
export function AgentWorkbenchLayout({ nav, config, debug }: AgentWorkbenchLayoutProps) {
  return (
    <div className="module-studio__agent-workbench-shell">
      <div className="module-studio__agent-workbench-layout">
        <div className="module-studio__agent-workbench-col module-studio__agent-workbench-col--nav">{nav}</div>
        <div className="module-studio__agent-workbench-col module-studio__agent-workbench-col--config">{config}</div>
        <div className="module-studio__agent-workbench-col module-studio__agent-workbench-col--debug">{debug}</div>
      </div>
    </div>
  );
}

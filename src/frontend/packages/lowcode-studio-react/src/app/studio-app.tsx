import React, { useEffect, useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Layout, Tabs, TabPane, Empty } from '@douyinfe/semi-ui';
import { LeftPanel } from '../panels/left-panel';
import { RightInspector } from '../panels/right-inspector';
import { TopToolbar } from '../panels/top-toolbar';
import { CanvasViewport } from '../panels/canvas-viewport';
import { WorkflowLeftPanel } from '../panels/workflow-left-panel';
import { WorkflowCanvas } from '../panels/workflow-canvas';
import { ShortcutPanel } from '../panels/shortcut-panel';
import { useStudioCommands } from '../hooks/use-studio-commands';
import { useDraftAutosave } from '../hooks/use-draft-autosave';
import { setLocale, t, type Locale } from '../i18n';
import { LowcodeStudioHostProvider, type LowcodeStudioHostConfig } from '../host';

const { Header, Sider, Content } = Layout;

// 包级单例 QueryClient，lowcode-studio-react 内部使用，不依赖宿主注入
const studioQueryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000,
    },
  },
});

/**
 * LowcodeStudioApp —— 低代码设计器三栏壳层（M07 C07-2）。
 *
 * - 顶部 TopToolbar：业务逻辑 / 用户界面切换 + 预览 / 调试 / 发布 / 版本 / 协作入口
 * - 左侧 LeftPanel：5 Tab（组件 / 模板 / 结构 / 数据 / 资源）
 * - 中部 CanvasViewport：画布（接 lowcode-editor-canvas）
 * - 右侧 RightInspector：三 Tab（属性 / 样式 / 事件）
 * - 全局：ShortcutPanel（Mod+/）+ useStudioCommands（Esc / Delete / Mod+S）
 */
export interface LowcodeStudioAppProps {
  appId: string;
  locale?: Locale;
  workspaceId?: string;
  workspaceLabel?: string;
  onBack?: () => void;
  host?: LowcodeStudioHostConfig;
}

export const LowcodeStudioApp: React.FC<LowcodeStudioAppProps> = ({ appId, locale, host, workspaceId, workspaceLabel }) => {
  const [topMode, setTopMode] = useState<'business' | 'ui'>('ui');

  useEffect(() => {
    if (locale) {
      setLocale(locale);
    }
  }, [locale]);

  // 必须在条件性 return 之前调用 hook，否则违反 React Rules of Hooks
  useStudioCommands({ appId });
  // P1-3：30s 去抖 autosave 兜底 + draftLock 心跳（PLAN §M04 S04-1 + S04-2）
  useDraftAutosave(appId);

  if (!appId) return <Empty title="缺少应用 ID" />;

  return (
    <LowcodeStudioHostProvider host={host}>
      <QueryClientProvider client={studioQueryClient}>
        <Layout style={{ height: '100vh' }}>
          <Header>
            <TopToolbar appId={appId} mode={topMode} onModeChange={setTopMode} />
          </Header>
          {topMode === 'ui' ? (
            <Layout>
              <Sider style={{ width: 280, background: '#f7f7f9', borderRight: '1px solid #eee' }}>
                <LeftPanel appId={appId} />
              </Sider>
              <Content>
                <CanvasViewport appId={appId} />
              </Content>
              <Sider style={{ width: 320, background: '#fff', borderLeft: '1px solid #eee' }}>
                <Tabs type="line" defaultActiveKey="property">
                  <TabPane tab={t('lowcode_studio.layout.right.property')} itemKey="property">
                    <RightInspector appId={appId} kind="property" />
                  </TabPane>
                  <TabPane tab={t('lowcode_studio.layout.right.style')} itemKey="style">
                    <RightInspector appId={appId} kind="style" />
                  </TabPane>
                  <TabPane tab={t('lowcode_studio.layout.right.events')} itemKey="events">
                    <RightInspector appId={appId} kind="events" />
                  </TabPane>
                </Tabs>
              </Sider>
            </Layout>
          ) : (
            <Layout>
              <Sider style={{ width: 280, background: '#f7f7f9', borderRight: '1px solid #eee' }}>
                <WorkflowLeftPanel appId={appId} />
              </Sider>
              <Content>
                <WorkflowCanvas appId={appId} workspaceId={workspaceId} workspaceLabel={workspaceLabel} />
              </Content>
            </Layout>
          )}
          <ShortcutPanel />
        </Layout>
      </QueryClientProvider>
    </LowcodeStudioHostProvider>
  );
};

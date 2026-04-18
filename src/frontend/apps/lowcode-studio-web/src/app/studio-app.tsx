import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { Layout, Tabs, TabPane, Empty } from '@douyinfe/semi-ui';
import { LeftPanel } from '../panels/left-panel';
import { RightInspector } from '../panels/right-inspector';
import { TopToolbar } from '../panels/top-toolbar';
import { CanvasViewport } from '../panels/canvas-viewport';
import { ShortcutPanel } from '../panels/shortcut-panel';
import { useStudioCommands } from '../hooks/use-studio-commands';
import { useDraftAutosave } from '../hooks/use-draft-autosave';
import { t } from '../i18n';

const { Header, Sider, Content } = Layout;

/**
 * StudioApp —— 三栏壳层（M07 C07-2）。
 *
 * - 顶部 TopToolbar：业务逻辑 / 用户界面切换 + 预览 / 调试 / 发布 / 版本 / 协作入口
 * - 左侧 LeftPanel：5 Tab（组件 / 模板 / 结构 / 数据 / 资源）
 * - 中部 CanvasViewport：画布（接 lowcode-editor-canvas）
 * - 右侧 RightInspector：三 Tab（属性 / 样式 / 事件）
 * - 全局：ShortcutPanel（Mod+/）+ useStudioCommands（Esc / Delete / Mod+S）
 */
export const StudioApp: React.FC = () => {
  const { appId } = useParams();
  const [topMode, setTopMode] = useState<'business' | 'ui'>('ui');

  // 必须在条件性 return 之前调用 hook，否则违反 React Rules of Hooks
  useStudioCommands({ appId: appId ?? '' });
  // P1-3：30s 去抖 autosave 兜底 + draftLock 心跳（PLAN §M04 S04-1 + S04-2）
  useDraftAutosave(appId);

  if (!appId) return <Empty title="缺少应用 ID" />;

  return (
    <Layout style={{ height: '100vh' }}>
      <Header>
        <TopToolbar appId={appId} mode={topMode} onModeChange={setTopMode} />
      </Header>
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
      <ShortcutPanel />
    </Layout>
  );
};

import React, { useState } from 'react';
import { Tabs, TabPane, Input, List, Typography } from '@douyinfe/semi-ui';
import { listShortcuts } from '@atlas/lowcode-editor-canvas';
import { t } from '../i18n';

/**
 * 左侧 5 Tab 面板（M07 C07-2 / C07-5 / C07-6 / C07-7）。
 *
 * - 组件：拉取 GET /api/v1/lowcode/components/registry 列表；远程检索 + 默认 20 条
 * - 模板：页面模板 / 组件组合 / 行业模板
 * - 结构：使用 @atlas/lowcode-editor-outline buildOutline（M07 阶段直接读 page.root）
 * - 数据：数据源管理（接 workflow output / 数据库快捷查询 / 静态 mock / 共享数据源）
 * - 资源：投射模式（M07 C07-5）—— 工作流 / 对话流 / 数据库 / 知识库 / 变量 / 会话 / 触发器 / 文件资产 / 插件 / 长期记忆 / 记忆库 / 提示词模板
 */
export const LeftPanel: React.FC<{ appId: string }> = ({ appId }) => {
  const [keyword, setKeyword] = useState('');

  return (
    <Tabs tabPosition="left" type="line" defaultActiveKey="components">
      <TabPane tab={t('lowcode_studio.layout.left.components')} itemKey="components">
        <Input prefix="🔍" placeholder="搜索组件" value={keyword} onChange={setKeyword} />
        <List size="small" style={{ marginTop: 8 }} dataSource={['Container', 'Row', 'Column', 'Button', 'TextInput', 'AiChat']} renderItem={(item) => <List.Item>{item}</List.Item>} />
      </TabPane>

      <TabPane tab={t('lowcode_studio.layout.left.templates')} itemKey="templates">
        <Typography.Paragraph>页面模板 / 组件组合 / 模式 A/B/C/D 模板（M07 装配中）</Typography.Paragraph>
      </TabPane>

      <TabPane tab={t('lowcode_studio.layout.left.outline')} itemKey="outline">
        <Typography.Paragraph>结构树（接入 @atlas/lowcode-editor-outline）</Typography.Paragraph>
        <Typography.Text type="secondary">app: {appId}</Typography.Text>
      </TabPane>

      <TabPane tab={t('lowcode_studio.layout.left.data')} itemKey="data">
        <Typography.Paragraph>数据源管理：workflow output / 数据库快捷查询 / 静态 mock / 共享数据源</Typography.Paragraph>
      </TabPane>

      <TabPane tab={t('lowcode_studio.layout.left.resources')} itemKey="resources">
        <Input prefix="🔍" placeholder={t('lowcode_studio.resources.search')} />
        <List
          size="small"
          style={{ marginTop: 8 }}
          dataSource={[
            t('lowcode_studio.resources.workflows'),
            t('lowcode_studio.resources.chatflows'),
            t('lowcode_studio.resources.databases'),
            t('lowcode_studio.resources.knowledge'),
            t('lowcode_studio.resources.plugins'),
            t('lowcode_studio.resources.promptTemplates'),
            t('lowcode_studio.resources.longTermMemory'),
            t('lowcode_studio.resources.memoryBank')
          ]}
          renderItem={(item) => <List.Item>{item}</List.Item>}
        />
        <Typography.Paragraph type="secondary" style={{ marginTop: 12 }}>
          快捷键 ≥ {listShortcuts().length} 项；按 Mod+/ 打开面板
        </Typography.Paragraph>
      </TabPane>
    </Tabs>
  );
};

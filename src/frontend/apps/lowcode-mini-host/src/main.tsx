import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { Card, List, Radio, RadioGroup, Typography } from '@douyinfe/semi-ui';
import { findUnsupportedComponents } from '@atlas/lowcode-runtime-mini';
import type { ComponentSchema, RendererType } from '@atlas/lowcode-schema';
import { t } from './i18n';

const { Title, Text } = Typography;

const DEMO_TREE: ComponentSchema = {
  id: 'root',
  type: 'Container',
  children: [
    { id: 'btn-1', type: 'Button' },
    { id: 'editor-1', type: 'CodeEditor' },
    { id: 'chart-1', type: 'Chart' },
    { id: 'aichat-1', type: 'AiChat' }
  ]
};

const RENDERERS: ReadonlyArray<RendererType> = ['mini-wx', 'mini-douyin', 'h5'];

const App: React.FC = () => {
  const [renderer, setRenderer] = useState<RendererType>('mini-wx');
  const unsupported = findUnsupportedComponents(DEMO_TREE, renderer);

  return (
    <div style={{ padding: 16, maxWidth: 720, margin: '0 auto' }}>
      <Card bodyStyle={{ padding: 24 }}>
        <Title heading={3} style={{ margin: 0 }}>
          {t('mini.title')}
        </Title>
        <Text type="tertiary" style={{ display: 'block', marginTop: 4 }}>
          {t('mini.currentRenderer')}：<strong>{renderer}</strong>。{t('mini.tip')}.
        </Text>

        <div style={{ marginTop: 16 }}>
          <RadioGroup
            type="button"
            value={renderer}
            onChange={(event) => setRenderer(event.target.value as RendererType)}
          >
            {RENDERERS.map((r) => (
              <Radio key={r} value={r}>
                {r}
              </Radio>
            ))}
          </RadioGroup>
        </div>

        <Title heading={5} style={{ marginTop: 24 }}>
          {t('mini.componentTree')}
        </Title>
        <List
          dataSource={DEMO_TREE.children ?? []}
          renderItem={(c) => {
            const degraded = unsupported.includes(c.id);
            return (
              <List.Item>
                <Text type={degraded ? 'danger' : 'success'}>
                  {c.type} ({c.id}) — {degraded ? t('mini.degraded') : t('mini.supported')}
                </Text>
              </List.Item>
            );
          }}
        />

        <Text type="tertiary" style={{ display: 'block', fontSize: 12, marginTop: 24 }}>
          {t('mini.h5OnlyNote')} <code>taro build --type weapp</code> /{' '}
          <code>taro build --type tt</code>
        </Text>
      </Card>
    </div>
  );
};

createRoot(document.getElementById('root') as HTMLElement).render(<App />);

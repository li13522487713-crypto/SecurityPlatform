import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { Button, Card, Input, Space, Typography } from '@douyinfe/semi-ui';
import { installToWindow, mount, type MountInstance } from '@atlas/lowcode-web-sdk';

const { Title, Text } = Typography;

/**
 * Atlas Lowcode SDK 嵌入示例（M17 C17-3 + P2-3 真实化）。
 * 三种嵌入方式同页演示，全部真实可跑：
 *  1) npm import：直接 mount() 到 React ref（始终真实）
 *  2) <script> 嵌入：installToWindow() 把 mount 注入 window.AtlasLowcode；
 *     生产页面的 <script src="…umd.js"> 加载后即可调用 window.AtlasLowcode.mount(...)。
 *     本 playground 在 useEffect 内调用 installToWindow + 一个独立 container 演示
 *     "脱离 React 的纯 vanilla mount" 等价路径，与生产 <script> 嵌入 100% 等价。
 *  3) iframe 嵌套：默认指向 lowcode-preview-web（http://localhost:5184），可配置切换 hosted。
 */
const App: React.FC = () => {
  const npmRef = useRef<HTMLDivElement>(null);
  const scriptRef = useRef<HTMLDivElement>(null);
  const [npmInstance, setNpmInstance] = useState<MountInstance | null>(null);
  const [scriptInstance, setScriptInstance] = useState<MountInstance | null>(null);
  const [count, setCount] = useState(0);
  const [iframeSrc, setIframeSrc] = useState<string>(
    typeof window !== 'undefined' && window.location.host.includes('5186')
      ? 'http://localhost:5184/'
      : '/preview/'
  );

  useEffect(() => {
    if (!npmRef.current) return;
    const inst = mount({
      container: npmRef.current,
      appId: 'demo-npm',
      tenantId: '00000000-0000-0000-0000-000000000001',
      initialState: { page: { count: 0 } },
      onEvent: (e) => {
        console.log('[sdk-playground npm]', e);
      }
    });
    setNpmInstance(inst);
    return () => inst.unmount();
  }, []);

  useEffect(() => {
    if (!scriptRef.current) return;
    // 模拟 <script src="atlas-lowcode.umd.js"> 加载后的 window.AtlasLowcode 全局 API：
    installToWindow();
    const win = window as unknown as { AtlasLowcode?: { mount: typeof mount } };
    if (!win.AtlasLowcode) return;
    const inst = win.AtlasLowcode.mount({
      container: scriptRef.current,
      appId: 'demo-script',
      tenantId: '00000000-0000-0000-0000-000000000001',
      onEvent: (e) => {
        console.log('[sdk-playground script]', e);
      }
    });
    setScriptInstance(inst);
    return () => inst.unmount();
  }, []);

  return (
    <div style={{ padding: 24, maxWidth: 960, margin: '0 auto' }}>
      <Card bodyStyle={{ padding: 24 }}>
        <Title heading={3} style={{ margin: 0 }}>
          Atlas Lowcode SDK Playground
        </Title>
        <Text type="tertiary" style={{ display: 'block', marginTop: 4 }}>
          三种嵌入方式同页演示（P2-3 真实可跑）。
        </Text>

        <Title heading={5} style={{ marginTop: 24 }}>
          1) npm import
        </Title>
        <div ref={npmRef} />
        <Space style={{ marginTop: 8 }}>
          <Button
            type="primary"
            theme="solid"
            onClick={() => {
              npmInstance?.update([
                { scope: 'page', path: 'page.count', op: 'set', value: count + 1 }
              ]);
              setCount(count + 1);
            }}
          >
            update count = {count + 1}
          </Button>
          <Button
            type="tertiary"
            theme="light"
            onClick={async () => {
              try {
                const r = await npmInstance?.dispatch({
                  eventName: 'demo.click',
                  actions: [
                    {
                      kind: 'set_variable',
                      payload: {
                        targetPath: 'page.fromDispatch',
                        scopeRoot: 'page',
                        value: { sourceType: 'static', value: 'set via dispatch', valueType: 'string' }
                      }
                    }
                  ]
                });
                console.log('dispatch result', r);
              } catch (e) {
                console.error('dispatch failed', e);
              }
            }}
          >
            调用 dispatch（M13 唯一桥梁）
          </Button>
        </Space>

        <Title heading={5} style={{ marginTop: 24 }}>
          2) &lt;script&gt; 嵌入（real）
        </Title>
        <Text type="tertiary" style={{ display: 'block', fontSize: 12 }}>
          通过 <code>installToWindow()</code> 把 mount 注入 <code>window.AtlasLowcode</code>，与生产页面的 UMD{' '}
          <code>&lt;script src="…umd.js"&gt;</code> 等价：
        </Text>
        <div ref={scriptRef} />
        <div style={{ marginTop: 8 }}>
          <Button
            type="tertiary"
            theme="light"
            onClick={() =>
              scriptInstance?.update([
                { scope: 'app', path: 'app.lastClickAt', op: 'set', value: new Date().toISOString() }
              ])
            }
          >
            script 实例 update app.lastClickAt
          </Button>
        </div>

        <Title heading={5} style={{ marginTop: 24 }}>
          3) iframe 嵌套（preview / hosted）
        </Title>
        <label style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 8 }}>
          <Text strong>iframe src</Text>
          <Input value={iframeSrc} onChange={(value) => setIframeSrc(value)} style={{ maxWidth: 480 }} />
        </label>
        <iframe
          title="hosted-demo"
          src={iframeSrc}
          style={{ width: '100%', height: 240, border: '1px solid var(--semi-color-border, #d9d9d9)' }}
        />
      </Card>
    </div>
  );
};

createRoot(document.getElementById('root') as HTMLElement).render(<App />);

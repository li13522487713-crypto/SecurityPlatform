import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { mount, type MountInstance } from '@atlas/lowcode-web-sdk';

/**
 * Atlas Lowcode SDK 嵌入示例（M17 C17-3）。
 * 三种嵌入方式同页演示：
 *  1) npm import：直接 mount() 到 React ref
 *  2) <script> 嵌入：在页面挂 window.AtlasLowcode + 一个独立 div 容器
 *  3) iframe 嵌入：iframe src 指向 hosted 应用 URL（M17 简化为 about:blank 占位）
 */
const App: React.FC = () => {
  const npmRef = useRef<HTMLDivElement>(null);
  const [npmInstance, setNpmInstance] = useState<MountInstance | null>(null);
  const [count, setCount] = useState(0);

  useEffect(() => {
    if (!npmRef.current) return;
    const inst = mount({
      container: npmRef.current,
      appId: 'demo-npm',
      tenantId: '00000000-0000-0000-0000-000000000001',
      initialState: { page: { count: 0 } },
      onEvent: (e) => {
        // eslint-disable-next-line no-console
        console.log('[sdk-playground npm]', e);
      }
    });
    setNpmInstance(inst);
    return () => inst.unmount();
  }, []);

  return (
    <div style={{ fontFamily: 'system-ui', padding: 24, maxWidth: 960, margin: '0 auto' }}>
      <h2>Atlas Lowcode SDK Playground</h2>
      <p style={{ color: '#666' }}>三种嵌入方式同页演示。</p>

      <h3>1) npm import</h3>
      <div ref={npmRef} />
      <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
        <button onClick={() => { npmInstance?.update([{ scope: 'page', path: 'page.count', op: 'set', value: count + 1 }]); setCount(count + 1); }}>
          update count = {count + 1}
        </button>
        <button onClick={async () => {
          try {
            const r = await npmInstance?.dispatch({
              eventName: 'demo.click',
              actions: [{ kind: 'set_variable', payload: { targetPath: 'page.fromDispatch', scopeRoot: 'page', value: { sourceType: 'static', value: 'set via dispatch', valueType: 'string' } } }]
            });
            // eslint-disable-next-line no-console
            console.log('dispatch result', r);
          } catch (e) {
            // eslint-disable-next-line no-console
            console.error('dispatch failed', e);
          }
        }}>
          调用 dispatch（M13 唯一桥梁）
        </button>
      </div>

      <h3 style={{ marginTop: 24 }}>2) &lt;script&gt; 嵌入</h3>
      <pre style={{ background: '#f5f5f5', padding: 12 }}>{`
<div id="lowcode-app"></div>
<script src="/atlas-lowcode.umd.js"></script>
<script>
  AtlasLowcode.mount({ container: '#lowcode-app', appId: 'demo', tenantId: '...' });
</script>
`}</pre>

      <h3>3) iframe 嵌套</h3>
      <iframe title="hosted-demo" src="about:blank" style={{ width: '100%', height: 200, border: '1px solid #d9d9d9' }} />
    </div>
  );
};

createRoot(document.getElementById('root') as HTMLElement).render(<App />);

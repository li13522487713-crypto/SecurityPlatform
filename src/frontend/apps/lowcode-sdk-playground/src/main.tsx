import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { installToWindow, mount, type MountInstance } from '@atlas/lowcode-web-sdk';

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
    <div style={{ fontFamily: 'system-ui', padding: 24, maxWidth: 960, margin: '0 auto' }}>
      <h2>Atlas Lowcode SDK Playground</h2>
      <p style={{ color: '#666' }}>三种嵌入方式同页演示（P2-3 真实可跑）。</p>

      <h3>1) npm import</h3>
      <div ref={npmRef} />
      <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
        <button onClick={() => { npmInstance?.update([{ scope: 'page', path: 'page.count', op: 'set', value: count + 1 }]); setCount(count + 1); }}>
          update count = {count + 1}
        </button>
        <        button onClick={async () => {
          try {
            const r = await npmInstance?.dispatch({
              eventName: 'demo.click',
              actions: [{ kind: 'set_variable', payload: { targetPath: 'page.fromDispatch', scopeRoot: 'page', value: { sourceType: 'static', value: 'set via dispatch', valueType: 'string' } } }]
            });
            console.log('dispatch result', r);
          } catch (e) {
            console.error('dispatch failed', e);
          }
        }}>
          调用 dispatch（M13 唯一桥梁）
        </button>
      </div>

      <h3 style={{ marginTop: 24 }}>2) &lt;script&gt; 嵌入（real）</h3>
      <p style={{ color: '#666', fontSize: 12 }}>
        通过 <code>installToWindow()</code> 把 mount 注入 <code>window.AtlasLowcode</code>，与生产页面的 UMD <code>&lt;script src="…umd.js"&gt;</code> 等价：
      </p>
      <div ref={scriptRef} />
      <div style={{ marginTop: 8 }}>
        <button onClick={() => scriptInstance?.update([{ scope: 'app', path: 'app.lastClickAt', op: 'set', value: new Date().toISOString() }])}>
          script 实例 update app.lastClickAt
        </button>
      </div>

      <h3 style={{ marginTop: 24 }}>3) iframe 嵌套（preview / hosted）</h3>
      <div style={{ marginBottom: 8 }}>
        <label>
          iframe src：
          <input
            value={iframeSrc}
            onChange={(e) => setIframeSrc(e.target.value)}
            style={{ width: 400, padding: 4, marginLeft: 8 }}
          />
        </label>
      </div>
      <iframe title="hosted-demo" src={iframeSrc} style={{ width: '100%', height: 240, border: '1px solid #d9d9d9' }} />
    </div>
  );
};

createRoot(document.getElementById('root') as HTMLElement).render(<App />);

import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { findUnsupportedComponents } from '@atlas/lowcode-runtime-mini';
import type { ComponentSchema, RendererType } from '@atlas/lowcode-schema';

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

const App: React.FC = () => {
  const [renderer, setRenderer] = useState<RendererType>('mini-wx');
  const unsupported = findUnsupportedComponents(DEMO_TREE, renderer);

  return (
    <div style={{ fontFamily: 'system-ui', padding: 16, maxWidth: 720, margin: '0 auto' }}>
      <h2>Atlas Lowcode Mini Host</h2>
      <p style={{ color: '#666' }}>当前渲染器：<strong>{renderer}</strong>。切换以查看降级提示。</p>
      <div style={{ marginBottom: 16 }}>
        {(['mini-wx', 'mini-douyin', 'h5'] as RendererType[]).map((r) => (
          <button key={r} onClick={() => setRenderer(r)} style={{ marginRight: 8, padding: '4px 12px', background: renderer === r ? '#1677ff' : '#f0f0f0', color: renderer === r ? '#fff' : '#000', border: 0, borderRadius: 4 }}>
            {r}
          </button>
        ))}
      </div>
      <h3>组件树</h3>
      <ul>
        {DEMO_TREE.children?.map((c) => (
          <li key={c.id} style={{ color: unsupported.includes(c.id) ? '#ff4d4f' : '#52c41a' }}>
            {c.type} ({c.id}) {unsupported.includes(c.id) ? '— 降级（不支持）' : '— 支持'}
          </li>
        ))}
      </ul>
      <p style={{ color: '#999', fontSize: 12, marginTop: 24 }}>
        本壳层为 H5 预览。微信 / 抖音小程序 build 由 Taro CLI 在运维流水线执行：<code>taro build --type weapp</code> / <code>taro build --type tt</code>。
      </p>
    </div>
  );
};

createRoot(document.getElementById('root') as HTMLElement).render(<App />);

/**
 * 布局类组件实现（M06 P1-1 + 8 件）：Container / Row / Column / Tabs / Drawer / Modal / Grid / Section
 *
 * 强约束（PLAN.md §1.3 #4）：
 * - 不得 fetch / import workflow client / 直调 /api/runtime/*
 * - 仅 React + Semi UI thin 包装；事件由 dispatch 统一委托
 */
import * as React from 'react';
import { Tabs, TabPane, SideSheet, Modal as SemiModal, Typography, Card } from '@douyinfe/semi-ui';
import type { ComponentRenderContext, ComponentRenderer } from './runtime-types';

const { Title } = Typography;

const Container: ComponentRenderer = ({ props, children }) => (
  <div className={asString(props.className)} style={asStyle(props.style)}>{children}</div>
);

const Row: ComponentRenderer = ({ props, children }) => (
  <div
    style={{
      display: 'flex',
      flexDirection: 'row',
      gap: asNumber(props.gap, 8),
      justifyContent: asString(props.justify, 'flex-start'),
      alignItems: asString(props.align, 'stretch')
    }}
  >
    {children}
  </div>
);

const Column: ComponentRenderer = ({ props, children }) => (
  <div
    style={{
      display: 'flex',
      flexDirection: 'column',
      gap: asNumber(props.gap, 8),
      justifyContent: asString(props.justify, 'flex-start'),
      alignItems: asString(props.align, 'stretch')
    }}
  >
    {children}
  </div>
);

const TabsImpl: ComponentRenderer = ({ schema, props, fireEvent }) => {
  const tabs = (schema.children ?? []).filter((c) => Boolean(c));
  return (
    <Tabs activeKey={asString(props.activeKey)} onChange={(key) => fireEvent('onChange', { key })}>
      {tabs.map((c, i) => (
        <TabPane key={c.id ?? String(i)} tab={asString(c.metadata?.title) ?? `Tab ${i + 1}`} itemKey={c.id ?? String(i)}>
          <ChildSlot child={c} />
        </TabPane>
      ))}
    </Tabs>
  );
};

const Drawer: ComponentRenderer = ({ props, children, fireEvent }) => (
  <SideSheet
    visible={asBoolean(props.visible)}
    placement={(asString(props.placement) as 'left' | 'right' | 'top' | 'bottom') ?? 'right'}
    title={asString(props.title)}
    onCancel={() => fireEvent('onChange', { visible: false })}
  >
    {children}
  </SideSheet>
);

const Modal: ComponentRenderer = ({ props, children, fireEvent }) => (
  <SemiModal
    visible={asBoolean(props.visible)}
    title={asString(props.title)}
    onCancel={() => fireEvent('onChange', { visible: false })}
    onOk={() => fireEvent('onSubmit', { visible: false })}
  >
    {children}
  </SemiModal>
);

const Grid: ComponentRenderer = ({ props, children }) => (
  <div
    style={{
      display: 'grid',
      gridTemplateColumns: `repeat(${asNumber(props.columns, 2)}, 1fr)`,
      gap: asNumber(props.gap, 8)
    }}
  >
    {children}
  </div>
);

const Section: ComponentRenderer = ({ props, children }) => (
  <Card title={<Title heading={5}>{asString(props.title)}</Title>}>{children}</Card>
);

export const LAYOUT_COMPONENTS: Record<string, ComponentRenderer> = {
  Container,
  Row,
  Column,
  Tabs: TabsImpl,
  Drawer,
  Modal,
  Grid,
  Section
};

// --- 内部辅助：避免在每个组件重复 prop 转换 ---

function asString(v: unknown, def?: string): string | undefined {
  if (typeof v === 'string') return v;
  if (typeof v === 'number' || typeof v === 'boolean') return String(v);
  return def;
}

function asNumber(v: unknown, def = 0): number {
  if (typeof v === 'number' && Number.isFinite(v)) return v;
  if (typeof v === 'string') {
    const n = Number(v);
    if (Number.isFinite(n)) return n;
  }
  return def;
}

function asBoolean(v: unknown, def = false): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'string') return v === 'true' || v === '1';
  if (typeof v === 'number') return v !== 0;
  return def;
}

function asStyle(v: unknown): React.CSSProperties | undefined {
  if (v && typeof v === 'object') return v as React.CSSProperties;
  return undefined;
}

// 子节点占位：lowcode-runtime-web 的 RuntimeRenderer 会通过 children 注入；
// 当本包独立测试时使用最小渲染避免 undefined。
const ChildSlot: React.FC<{ child: ComponentRenderContext['schema'] }> = ({ child }) => (
  <div data-component-type={child.type} data-component-id={child.id} />
);

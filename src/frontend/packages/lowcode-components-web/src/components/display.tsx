/**
 * 展示类组件实现（M06 P1-1，13 件）：
 * Text / Markdown / Image / Video / Avatar / Badge / Progress / Rate / Chart / EmptyState / Loading / Error / Toast
 */
import * as React from 'react';
import {
  Avatar,
  Badge,
  Button as SemiButton,
  Empty,
  Progress,
  Rating,
  Spin,
  Toast as SemiToast,
  Typography
} from '@douyinfe/semi-ui';
import { IconAlertTriangle } from '@douyinfe/semi-icons';
import type { ComponentRenderer } from './runtime-types';

const { Text: SemiText, Paragraph } = Typography;

const Text: ComponentRenderer = ({ props, getContentParam }) => {
  // 优先使用内容参数 text 渲染（含 i18n / 模板）；缺省回退 props.content
  const contentParam = getContentParam?.('text') ?? props.content;
  return <SemiText style={{ color: typeof props.color === 'string' ? props.color : undefined }}>{stringify(contentParam)}</SemiText>;
};

const Markdown: ComponentRenderer = ({ props, getContentParam }) => {
  // 简化：直接显示文本；真实 Markdown 渲染由调用方注入 markdown-it 等库以避免本包包体膨胀
  const text = getContentParam?.('text') ?? props.content;
  return <Paragraph style={{ whiteSpace: 'pre-wrap' }}>{stringify(text)}</Paragraph>;
};

const Image: ComponentRenderer = ({ props, getContentParam }) => {
  const src = getContentParam?.('image') ?? props.src;
  if (typeof src !== 'string') return <Empty description="无图片" />;
  return (
    <img
      src={src}
      alt={typeof props.alt === 'string' ? props.alt : ''}
      style={{ objectFit: (props.fit as 'cover' | 'contain' | undefined) ?? 'cover', maxWidth: '100%' }}
    />
  );
};

const Video: ComponentRenderer = ({ props, getContentParam }) => {
  const src = getContentParam?.('media') ?? props.src;
  if (typeof src !== 'string') return <Empty description="无视频源" />;
  return (
    <video
      src={src}
      poster={typeof props.poster === 'string' ? props.poster : undefined}
      autoPlay={Boolean(props.autoplay)}
      controls={props.controls !== false}
      style={{ maxWidth: '100%' }}
    />
  );
};

const AvatarImpl: ComponentRenderer = ({ props, getContentParam }) => {
  const src = getContentParam?.('image') ?? props.src;
  return (
    <Avatar
      src={typeof src === 'string' ? src : undefined}
      size={(props.size as 'small' | 'default' | 'medium' | 'large' | 'extra-large' | undefined) ?? 'default'}
      alt={typeof props.name === 'string' ? props.name : undefined}
    >
      {typeof props.name === 'string' ? props.name.slice(0, 1) : ''}
    </Avatar>
  );
};

const BadgeImpl: ComponentRenderer = ({ props, children }) => (
  <Badge count={typeof props.count === 'number' ? props.count : 0} type={(props.color as 'primary' | 'secondary' | 'tertiary' | 'danger' | 'warning' | 'success' | undefined) ?? 'primary'}>
    {children ?? null}
  </Badge>
);

const ProgressImpl: ComponentRenderer = ({ props }) => (
  <Progress
    percent={typeof props.percent === 'number' ? props.percent : 0}
    type={(props.status as 'default' | 'success' | 'warning' | 'danger' | undefined) ?? 'default'}
    showInfo
  />
);

const RateImpl: ComponentRenderer = ({ props, fireEvent }) => (
  <Rating
    value={typeof props.value === 'number' ? props.value : 0}
    count={typeof props.count === 'number' ? props.count : 5}
    onChange={(v) => fireEvent('onChange', { value: v })}
  />
);

const Chart: ComponentRenderer = ({ getContentParam }) => {
  // 真实 Chart 渲染由调用方注入 ECharts/G2 等库。当前以"数据预览"代替（dev/preview 友好）
  const data = getContentParam?.('data');
  return (
    <div style={{ padding: 12, border: '1px dashed var(--semi-color-border)', borderRadius: 4 }}>
      <SemiText type="tertiary" size="small">Chart 数据预览：</SemiText>
      <pre style={{ margin: 0, maxHeight: 160, overflow: 'auto' }}>{JSON.stringify(data ?? null, null, 2)}</pre>
    </div>
  );
};

const EmptyState: ComponentRenderer = ({ props }) => (
  <Empty
    title={typeof props.title === 'string' ? props.title : '暂无数据'}
    description={typeof props.description === 'string' ? props.description : undefined}
  />
);

const Loading: ComponentRenderer = ({ props }) => (
  <Spin size={(props.size as 'small' | 'middle' | 'large' | undefined) ?? 'middle'} />
);

const ErrorImpl: ComponentRenderer = ({ props, fireEvent }) => (
  <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--semi-color-danger)' }}>
    <IconAlertTriangle />
    <SemiText type="danger">{typeof props.message === 'string' ? props.message : '加载失败'}</SemiText>
    {props.retryable ? (
      <SemiButton size="small" onClick={() => fireEvent('onClick', { retry: true })}>
        重试
      </SemiButton>
    ) : null}
  </div>
);

// Toast 是命令式 API；这里以 inline 提示框形式提供"自动消失"展示，符合可视化设计期需要
const ToastImpl: ComponentRenderer = ({ props }) => {
  React.useEffect(() => {
    if (typeof props.message !== 'string' || props.message.length === 0) return;
    SemiToast[(props.type as 'info' | 'success' | 'warning' | 'error' | undefined) ?? 'info']({
      content: props.message,
      duration: typeof props.duration === 'number' ? props.duration : 3
    });
  }, [props.message, props.type, props.duration]);
  return null;
};

export const DISPLAY_COMPONENTS: Record<string, ComponentRenderer> = {
  Text,
  Markdown,
  Image,
  Video,
  Avatar: AvatarImpl,
  Badge: BadgeImpl,
  Progress: ProgressImpl,
  Rate: RateImpl,
  Chart,
  EmptyState,
  Loading,
  Error: ErrorImpl,
  Toast: ToastImpl
};

function stringify(v: unknown): string {
  if (v == null) return '';
  if (typeof v === 'string') return v;
  if (typeof v === 'number' || typeof v === 'boolean') return String(v);
  try {
    return JSON.stringify(v);
  } catch {
    return String(v);
  }
}

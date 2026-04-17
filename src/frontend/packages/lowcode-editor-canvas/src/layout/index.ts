/**
 * 三种 LayoutEngine（M04 C04-2）：自由 / 流式 / 响应式。
 *
 * 输入：组件矩形列表 + 容器尺寸 + 布局类型；输出：调整后的矩形列表。
 * 纯函数，便于单测 + 多端复用（M15 mini 端复用此抽象）。
 */

import type { PageLayout } from '@atlas/lowcode-schema';

export interface ComponentBox {
  id: string;
  x: number;
  y: number;
  width: number;
  height: number;
  /** 流式布局下的折行权重（数值越大越优先独占一行）。*/
  flowWeight?: number;
  /** 响应式布局：相对断点宽度（百分比）。*/
  responsiveSpan?: number;
}

export interface LayoutContext {
  containerWidth: number;
  containerHeight: number;
  /** 流式布局水平间距，默认 8。*/
  gap?: number;
}

export interface LayoutEngine {
  layoutType: PageLayout;
  layout(boxes: ReadonlyArray<ComponentBox>, ctx: LayoutContext): ComponentBox[];
}

const FreeLayoutEngine: LayoutEngine = {
  layoutType: 'free',
  layout: (boxes) => boxes.map((b) => ({ ...b }))
};

const FlowLayoutEngine: LayoutEngine = {
  layoutType: 'flow',
  layout: (boxes, ctx) => {
    const gap = ctx.gap ?? 8;
    let cursorX = 0;
    let cursorY = 0;
    let lineHeight = 0;
    return boxes.map((b) => {
      const independentLine = (b.flowWeight ?? 0) >= 1;
      if (independentLine || cursorX + b.width > ctx.containerWidth) {
        cursorX = 0;
        cursorY += lineHeight + gap;
        lineHeight = 0;
      }
      const placed = { ...b, x: cursorX, y: cursorY };
      cursorX += b.width + gap;
      lineHeight = Math.max(lineHeight, b.height);
      return placed;
    });
  }
};

const ResponsiveLayoutEngine: LayoutEngine = {
  layoutType: 'responsive',
  layout: (boxes, ctx) => {
    const gap = ctx.gap ?? 8;
    // 简单 12 栅格响应式：每个组件按 responsiveSpan 占 1-12 列
    const columns = 12;
    const colWidth = (ctx.containerWidth - gap * (columns - 1)) / columns;
    let cursorCol = 0;
    let cursorY = 0;
    let lineHeight = 0;
    return boxes.map((b) => {
      const span = Math.min(Math.max(b.responsiveSpan ?? 12, 1), 12);
      if (cursorCol + span > columns) {
        cursorCol = 0;
        cursorY += lineHeight + gap;
        lineHeight = 0;
      }
      const placed = {
        ...b,
        x: cursorCol * (colWidth + gap),
        y: cursorY,
        width: colWidth * span + gap * (span - 1),
        height: b.height
      };
      cursorCol += span;
      lineHeight = Math.max(lineHeight, b.height);
      return placed;
    });
  }
};

const ENGINES = new Map<PageLayout, LayoutEngine>([
  ['free', FreeLayoutEngine],
  ['flow', FlowLayoutEngine],
  ['responsive', ResponsiveLayoutEngine]
]);

export function getLayoutEngine(layout: PageLayout): LayoutEngine {
  const e = ENGINES.get(layout);
  if (!e) throw new Error(`未知布局：${layout}`);
  return e;
}

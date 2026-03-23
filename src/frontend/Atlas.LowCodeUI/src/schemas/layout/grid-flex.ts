/**
 * 【布局 IV-4.2 栅格】
 * Grid / Flex 12 栏响应式栅格 Schema Builder
 */
import type { AmisSchema } from "@/types/amis";

/** Grid 列定义 */
export interface GridColumnDef {
  body: AmisSchema | AmisSchema[];
  /** 大屏占列数（1-12） */
  md?: number;
  /** 中屏占列数 */
  sm?: number;
  /** 小屏占列数 */
  xs?: number;
  /** 列宽（CSS） */
  columnClassName?: string;
  /** 对齐方式 */
  valign?: "top" | "middle" | "bottom";
}

/**
 * 创建 Grid 栅格布局
 *
 * @example
 * ```ts
 * gridSchema([
 *   { body: statSchema(...), md: 3 },
 *   { body: statSchema(...), md: 3 },
 *   { body: chartSchema(...), md: 6 },
 * ])
 * ```
 */
export function gridSchema(columns: GridColumnDef[], opts: {
  gap?: string;
  valign?: "top" | "middle" | "bottom";
  align?: "left" | "center" | "right" | "between";
  className?: string;
} = {}): AmisSchema {
  return {
    type: "grid",
    columns: columns.map((col) => ({
      body: col.body,
      ...(col.md ? { md: col.md } : {}),
      ...(col.sm ? { sm: col.sm } : {}),
      ...(col.xs ? { xs: col.xs } : {}),
      ...(col.columnClassName ? { columnClassName: col.columnClassName } : {}),
      ...(col.valign ? { valign: col.valign } : {}),
    })),
    ...(opts.gap ? { gap: opts.gap } : {}),
    ...(opts.valign ? { valign: opts.valign } : {}),
    ...(opts.align ? { align: opts.align } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

/**
 * 创建等宽栅格（自动计算每列宽度）
 */
export function equalGrid(items: Array<AmisSchema | AmisSchema[]>, opts: {
  gap?: string;
  className?: string;
} = {}): AmisSchema {
  const md = Math.floor(12 / items.length);
  return gridSchema(
    items.map((body) => ({ body, md })),
    opts,
  );
}

/**
 * 创建 Flex 容器
 */
export function flexSchema(items: AmisSchema[], opts: {
  direction?: "row" | "column" | "row-reverse" | "column-reverse";
  justify?: "start" | "flex-start" | "center" | "end" | "flex-end" | "space-around" | "space-between" | "space-evenly";
  alignItems?: "stretch" | "start" | "flex-start" | "center" | "end" | "flex-end" | "baseline";
  wrap?: "nowrap" | "wrap" | "wrap-reverse";
  gap?: string;
  className?: string;
  style?: Record<string, string>;
} = {}): AmisSchema {
  return {
    type: "flex",
    items,
    ...(opts.direction ? { direction: opts.direction } : {}),
    ...(opts.justify ? { justify: opts.justify } : {}),
    ...(opts.alignItems ? { alignItems: opts.alignItems } : {}),
    ...(opts.wrap ? { wrap: opts.wrap } : {}),
    ...(opts.gap ? { style: { ...(opts.style ?? {}), gap: opts.gap } } : opts.style ? { style: opts.style } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

// ========== 预置仪表盘布局 ==========

/** 3 列仪表盘布局 */
export function dashboard3Col(items: [AmisSchema, AmisSchema, AmisSchema]): AmisSchema {
  return gridSchema([
    { body: items[0], md: 4 },
    { body: items[1], md: 4 },
    { body: items[2], md: 4 },
  ]);
}

/** 4 列仪表盘布局 */
export function dashboard4Col(items: [AmisSchema, AmisSchema, AmisSchema, AmisSchema]): AmisSchema {
  return gridSchema([
    { body: items[0], md: 3 },
    { body: items[1], md: 3 },
    { body: items[2], md: 3 },
    { body: items[3], md: 3 },
  ]);
}

/** 左 8 右 4 双栏布局 */
export function sidebar84(main: AmisSchema, side: AmisSchema): AmisSchema {
  return gridSchema([
    { body: main, md: 8 },
    { body: side, md: 4 },
  ]);
}

/** 左 4 右 8 双栏布局 */
export function sidebar48(side: AmisSchema, main: AmisSchema): AmisSchema {
  return gridSchema([
    { body: side, md: 4 },
    { body: main, md: 8 },
  ]);
}

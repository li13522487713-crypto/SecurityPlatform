/**
 * 【数据展示 III-3.2 图表】
 * ECharts 图表 Schema 工厂：柱状图/折线图/饼图/散点图
 * 支持 api 数据源与静态数据两种模式
 */
import type { AmisSchema } from "@/types/amis";

/** 图表通用选项 */
export interface ChartBaseOptions {
  /** 图表名称 */
  name?: string;
  /** 宽度 */
  width?: number | string;
  /** 高度（默认 300） */
  height?: number | string;
  /** 数据接口 */
  api?: string | Record<string, unknown>;
  /** 轮询间隔（毫秒，0 表示不轮询） */
  interval?: number;
  /** 附加 className */
  className?: string;
  /** 静态配置对象 */
  config?: Record<string, unknown>;
}

/** 构建图表 Schema 的基础函数 */
function chartBase(chartConfig: Record<string, unknown>, opts: ChartBaseOptions): AmisSchema {
  return {
    type: "chart",
    ...(opts.name ? { name: opts.name } : {}),
    ...(opts.width ? { width: opts.width } : {}),
    height: opts.height ?? 300,
    ...(opts.api ? { api: opts.api } : {}),
    ...(opts.interval ? { interval: opts.interval } : {}),
    ...(opts.className ? { className: opts.className } : {}),
    config: opts.config ?? chartConfig,
  };
}

// ========== 柱状图 ==========

export interface BarChartOptions extends ChartBaseOptions {
  /** X 轴数据 */
  xAxisData?: string[];
  /** 系列数据，每个系列一个数组 */
  series?: Array<{ name: string; data: number[] }>;
  /** 是否堆叠 */
  stack?: boolean;
  /** 是否水平方向 */
  horizontal?: boolean;
  /** 标题 */
  title?: string;
}

/** 柱状图 */
export function barChart(opts: BarChartOptions = {}): AmisSchema {
  const xAxis = opts.horizontal
    ? { type: "value" as const }
    : { type: "category" as const, data: opts.xAxisData ?? [] };
  const yAxis = opts.horizontal
    ? { type: "category" as const, data: opts.xAxisData ?? [] }
    : { type: "value" as const };

  return chartBase({
    ...(opts.title ? { title: { text: opts.title } } : {}),
    tooltip: { trigger: "axis" },
    legend: {},
    xAxis,
    yAxis,
    series: (opts.series ?? []).map((s) => ({
      name: s.name,
      type: "bar",
      data: s.data,
      ...(opts.stack ? { stack: "total" } : {}),
    })),
  }, opts);
}

// ========== 折线图 ==========

export interface LineChartOptions extends ChartBaseOptions {
  xAxisData?: string[];
  series?: Array<{ name: string; data: number[]; smooth?: boolean; areaStyle?: boolean }>;
  title?: string;
}

/** 折线图 */
export function lineChart(opts: LineChartOptions = {}): AmisSchema {
  return chartBase({
    ...(opts.title ? { title: { text: opts.title } } : {}),
    tooltip: { trigger: "axis" },
    legend: {},
    xAxis: { type: "category", data: opts.xAxisData ?? [] },
    yAxis: { type: "value" },
    series: (opts.series ?? []).map((s) => ({
      name: s.name,
      type: "line",
      data: s.data,
      ...(s.smooth ? { smooth: true } : {}),
      ...(s.areaStyle ? { areaStyle: {} } : {}),
    })),
  }, opts);
}

// ========== 饼图 ==========

export interface PieChartOptions extends ChartBaseOptions {
  data?: Array<{ name: string; value: number }>;
  title?: string;
  radius?: string | [string, string];
  roseType?: "radius" | "area";
}

/** 饼图 */
export function pieChart(opts: PieChartOptions = {}): AmisSchema {
  return chartBase({
    ...(opts.title ? { title: { text: opts.title } } : {}),
    tooltip: { trigger: "item" },
    legend: { orient: "vertical", left: "left" },
    series: [{
      type: "pie",
      radius: opts.radius ?? "50%",
      data: opts.data ?? [],
      ...(opts.roseType ? { roseType: opts.roseType } : {}),
      emphasis: {
        itemStyle: {
          shadowBlur: 10,
          shadowOffsetX: 0,
          shadowColor: "rgba(0, 0, 0, 0.5)",
        },
      },
    }],
  }, opts);
}

// ========== 散点图 ==========

export interface ScatterChartOptions extends ChartBaseOptions {
  series?: Array<{ name: string; data: Array<[number, number]> }>;
  title?: string;
  xAxisName?: string;
  yAxisName?: string;
}

/** 散点图 */
export function scatterChart(opts: ScatterChartOptions = {}): AmisSchema {
  return chartBase({
    ...(opts.title ? { title: { text: opts.title } } : {}),
    tooltip: { trigger: "item" },
    legend: {},
    xAxis: { type: "value", ...(opts.xAxisName ? { name: opts.xAxisName } : {}) },
    yAxis: { type: "value", ...(opts.yAxisName ? { name: opts.yAxisName } : {}) },
    series: (opts.series ?? []).map((s) => ({
      name: s.name,
      type: "scatter",
      data: s.data,
    })),
  }, opts);
}

// ========== 使用 API 数据源的图表 ==========

/**
 * 创建使用 API 数据源的图表（AMIS 会通过 api 获取 config 并渲染）
 *
 * @example
 * ```ts
 * apiChart({
 *   api: '/api/v1/dashboard/chart-data',
 *   height: 400,
 *   interval: 30000, // 30秒自动刷新
 * })
 * ```
 */
export function apiChart(opts: ChartBaseOptions & { trackExpression?: string } = {}): AmisSchema {
  return {
    type: "chart",
    ...(opts.name ? { name: opts.name } : {}),
    ...(opts.width ? { width: opts.width } : {}),
    height: opts.height ?? 300,
    ...(opts.api ? { api: opts.api } : {}),
    ...(opts.interval ? { interval: opts.interval } : {}),
    ...(opts.className ? { className: opts.className } : {}),
    ...(opts.trackExpression ? { trackExpression: opts.trackExpression } : {}),
  };
}

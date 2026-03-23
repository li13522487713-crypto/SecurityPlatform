/**
 * 【数据展示 III-3.2 统计值】
 * stat / timeline / progress / tag / calendar + DashboardPanel 组合 Schema
 */
import type { AmisSchema } from "@/types/amis";

// ========== 统计值 ==========

/** 统计卡片 */
export function statSchema(opts: {
  label: string;
  value: string | number;
  unit?: string;
  icon?: string;
  remark?: string;
  className?: string;
}): AmisSchema {
  return {
    type: "tpl",
    className: opts.className ?? "stat-card",
    tpl: [
      `<div style="padding:16px;background:#fff;border-radius:8px;box-shadow:0 1px 2px rgba(0,0,0,.06)">`,
      opts.icon ? `<i class="${opts.icon}" style="font-size:24px;color:#0052d9;margin-bottom:8px;display:block"></i>` : "",
      `<div style="font-size:12px;color:#86909c;margin-bottom:4px">${opts.label}</div>`,
      `<div style="font-size:28px;font-weight:600;color:#1d2129">${opts.value}${opts.unit ? `<span style="font-size:14px;font-weight:400;margin-left:4px">${opts.unit}</span>` : ""}</div>`,
      opts.remark ? `<div style="font-size:12px;color:#86909c;margin-top:4px">${opts.remark}</div>` : "",
      `</div>`,
    ].join(""),
  };
}

// ========== 时间线 ==========

export function timelineSchema(opts: {
  items: Array<{
    title: string;
    time?: string;
    detail?: string;
    color?: string;
    icon?: string;
  }>;
  mode?: "left" | "right" | "alternate";
  className?: string;
}): AmisSchema {
  return {
    type: "timeline",
    items: opts.items.map((item) => ({
      title: item.title,
      ...(item.time ? { time: item.time } : {}),
      ...(item.detail ? { detail: item.detail } : {}),
      ...(item.color ? { color: item.color } : {}),
      ...(item.icon ? { icon: item.icon } : {}),
    })),
    ...(opts.mode ? { mode: opts.mode } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

// ========== 进度条 ==========

export function progressSchema(opts: {
  value?: number;
  name?: string;
  mode?: "line" | "circle" | "dashboard";
  showLabel?: boolean;
  map?: Array<string>;
  stripe?: boolean;
  animate?: boolean;
  className?: string;
}): AmisSchema {
  return {
    type: "progress",
    ...(opts.name ? { name: opts.name } : {}),
    ...(opts.value !== undefined ? { value: opts.value } : {}),
    mode: opts.mode ?? "line",
    ...(opts.showLabel !== false ? { showLabel: true } : { showLabel: false }),
    ...(opts.map ? { map: opts.map } : {}),
    ...(opts.stripe ? { stripe: true } : {}),
    ...(opts.animate ? { animate: true } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

// ========== 标签 ==========

export function tagSchema(opts: {
  label: string;
  color?: string;
  displayMode?: "normal" | "rounded" | "status";
  closable?: boolean;
  icon?: string;
  className?: string;
}): AmisSchema {
  return {
    type: "tag",
    label: opts.label,
    ...(opts.color ? { color: opts.color } : {}),
    ...(opts.displayMode ? { displayMode: opts.displayMode } : {}),
    ...(opts.closable ? { closable: true } : {}),
    ...(opts.icon ? { icon: opts.icon } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

// ========== 状态映射 ==========

export function mappingSchema(opts: {
  name: string;
  map: Record<string, string>;
  label?: string;
  placeholder?: string;
}): AmisSchema {
  return {
    type: "mapping",
    name: opts.name,
    map: opts.map,
    ...(opts.label ? { label: opts.label } : {}),
    ...(opts.placeholder ? { placeholder: opts.placeholder } : {}),
  };
}

// ========== 日历 ==========

export function calendarSchema(opts: {
  scheduleClassNames?: string[];
  todayActiveStyle?: Record<string, string>;
  scheduleAction?: AmisSchema;
  className?: string;
} = {}): AmisSchema {
  return {
    type: "calendar",
    ...(opts.scheduleClassNames ? { scheduleClassNames: opts.scheduleClassNames } : {}),
    ...(opts.todayActiveStyle ? { todayActiveStyle: opts.todayActiveStyle } : {}),
    ...(opts.scheduleAction ? { scheduleAction: opts.scheduleAction } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

// ========== DashboardPanel 组合 ==========

/**
 * 仪表盘面板：stat 卡片行 + chart 区域的栅格组合
 *
 * @example
 * ```ts
 * dashboardPanel({
 *   stats: [
 *     { label: '总用户', value: '12,345', icon: 'fa fa-users' },
 *     { label: '活跃用户', value: '3,456', icon: 'fa fa-user-check' },
 *   ],
 *   charts: [
 *     barChart({ title: '月度趋势', ... }),
 *     pieChart({ title: '用户分布', ... }),
 *   ],
 * })
 * ```
 */
export function dashboardPanel(opts: {
  stats: Array<{ label: string; value: string | number; unit?: string; icon?: string; remark?: string }>;
  charts?: AmisSchema[];
  statsColumns?: number;
  chartColumns?: number;
}): AmisSchema {
  const body: AmisSchema[] = [];

  // stat 卡片行
  if (opts.stats.length > 0) {
    body.push({
      type: "grid",
      columns: opts.stats.map((s) => ({
        body: statSchema(s),
        md: Math.floor(12 / (opts.statsColumns ?? opts.stats.length)),
      })),
    });
  }

  // chart 区域
  if (opts.charts && opts.charts.length > 0) {
    body.push({
      type: "grid",
      columns: opts.charts.map((c) => ({
        body: c,
        md: Math.floor(12 / (opts.chartColumns ?? opts.charts!.length)),
      })),
    });
  }

  return {
    type: "page",
    body,
  };
}

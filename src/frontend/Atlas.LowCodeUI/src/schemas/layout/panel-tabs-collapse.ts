/**
 * 【布局 IV-4.2 面板/标签页/折叠】
 * Panel / Tabs / Collapse Schema Builder
 */
import type { AmisSchema } from "@/types/amis";

// ========== Panel ==========

export function panelSchema(opts: {
  title?: string;
  body: AmisSchema | AmisSchema[];
  footer?: AmisSchema | AmisSchema[];
  actions?: AmisSchema[];
  affixFooter?: boolean;
  headerClassName?: string;
  bodyClassName?: string;
  footerClassName?: string;
  className?: string;
}): AmisSchema {
  return {
    type: "panel",
    ...(opts.title ? { title: opts.title } : {}),
    body: opts.body,
    ...(opts.footer ? { footer: opts.footer } : {}),
    ...(opts.actions ? { actions: opts.actions } : {}),
    ...(opts.affixFooter ? { affixFooter: true } : {}),
    ...(opts.headerClassName ? { headerClassName: opts.headerClassName } : {}),
    ...(opts.bodyClassName ? { bodyClassName: opts.bodyClassName } : {}),
    ...(opts.footerClassName ? { footerClassName: opts.footerClassName } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

// ========== Tabs ==========

export interface TabItem {
  title: string;
  body: AmisSchema | AmisSchema[];
  icon?: string;
  hash?: string;
  tab?: AmisSchema;
  /** 懒加载：仅在切换到该 Tab 时才加载内容 */
  reload?: boolean;
  /** 是否禁用 */
  disabled?: boolean;
  /** 是否可关闭 */
  closable?: boolean;
  /** 动态标题表达式 */
  titleOn?: string;
  /** 未读消息 badge */
  badge?: number | string;
  className?: string;
}

export function tabsSchema(opts: {
  tabs: TabItem[];
  tabsMode?: "line" | "card" | "radio" | "vertical" | "chrome" | "simple" | "strong" | "tiled" | "sidebar";
  defaultKey?: string | number;
  mountOnEnter?: boolean;
  unmountOnExit?: boolean;
  closable?: boolean;
  draggable?: boolean;
  addable?: boolean;
  addBtnText?: string;
  className?: string;
  linksClassName?: string;
  contentClassName?: string;
}): AmisSchema {
  return {
    type: "tabs",
    tabs: opts.tabs.map((tab) => ({
      title: tab.title,
      body: tab.body,
      ...(tab.icon ? { icon: tab.icon } : {}),
      ...(tab.hash ? { hash: tab.hash } : {}),
      ...(tab.tab ? { tab: tab.tab } : {}),
      ...(tab.reload ? { reload: true } : {}),
      ...(tab.disabled ? { disabled: true } : {}),
      ...(tab.closable ? { closable: true } : {}),
      ...(tab.badge !== undefined ? { badge: tab.badge } : {}),
      ...(tab.className ? { className: tab.className } : {}),
    })),
    ...(opts.tabsMode ? { tabsMode: opts.tabsMode } : {}),
    ...(opts.defaultKey !== undefined ? { defaultKey: opts.defaultKey } : {}),
    ...(opts.mountOnEnter ? { mountOnEnter: true } : {}),
    ...(opts.unmountOnExit ? { unmountOnExit: true } : {}),
    ...(opts.closable ? { closable: true } : {}),
    ...(opts.draggable ? { draggable: true } : {}),
    ...(opts.addable ? { addable: true } : {}),
    ...(opts.addBtnText ? { addBtnText: opts.addBtnText } : {}),
    ...(opts.className ? { className: opts.className } : {}),
    ...(opts.linksClassName ? { linksClassName: opts.linksClassName } : {}),
    ...(opts.contentClassName ? { contentClassName: opts.contentClassName } : {}),
  };
}

// ========== Collapse ==========

export interface CollapseItem {
  header: string;
  body: AmisSchema | AmisSchema[];
  key?: string;
  disabled?: boolean;
  className?: string;
}

export function collapseGroupSchema(opts: {
  body: CollapseItem[];
  activeKey?: string | string[];
  accordion?: boolean;
  expandIconPosition?: "left" | "right";
  className?: string;
}): AmisSchema {
  return {
    type: "collapse-group",
    body: opts.body.map((item) => ({
      type: "collapse",
      header: item.header,
      body: item.body,
      ...(item.key ? { key: item.key } : {}),
      ...(item.disabled ? { disabled: true } : {}),
      ...(item.className ? { className: item.className } : {}),
    })),
    ...(opts.activeKey ? { activeKey: opts.activeKey } : {}),
    ...(opts.accordion ? { accordion: true } : {}),
    ...(opts.expandIconPosition ? { expandIconPosition: opts.expandIconPosition } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

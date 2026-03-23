/**
 * 【布局 IV-4.2 弹窗/抽屉】
 * Dialog / Drawer Schema Builder
 */
import type { AmisSchema } from "@/types/amis";

// ========== Dialog ==========

export interface DialogSchemaOptions {
  title: string;
  body: AmisSchema | AmisSchema[];
  size?: "xs" | "sm" | "md" | "lg" | "xl" | "full";
  actions?: AmisSchema[];
  closeOnEsc?: boolean;
  closeOnOutside?: boolean;
  showCloseButton?: boolean;
  showErrorMsg?: boolean;
  className?: string;
  bodyClassName?: string;
  headerClassName?: string;
  data?: Record<string, unknown>;
}

/**
 * 创建 Dialog 弹窗 Schema
 */
export function dialogSchema(opts: DialogSchemaOptions): AmisSchema {
  return {
    type: "dialog",
    title: opts.title,
    body: opts.body,
    ...(opts.size ? { size: opts.size } : {}),
    ...(opts.actions ? { actions: opts.actions } : {}),
    ...(opts.closeOnEsc !== false ? { closeOnEsc: opts.closeOnEsc ?? true } : {}),
    ...(opts.closeOnOutside ? { closeOnOutside: true } : {}),
    ...(opts.showCloseButton !== false ? {} : { showCloseButton: false }),
    ...(opts.showErrorMsg !== undefined ? { showErrorMsg: opts.showErrorMsg } : {}),
    ...(opts.className ? { className: opts.className } : {}),
    ...(opts.bodyClassName ? { bodyClassName: opts.bodyClassName } : {}),
    ...(opts.headerClassName ? { headerClassName: opts.headerClassName } : {}),
    ...(opts.data ? { data: opts.data } : {}),
  };
}

// ========== Drawer ==========

export interface DrawerSchemaOptions {
  title: string;
  body: AmisSchema | AmisSchema[];
  size?: "xs" | "sm" | "md" | "lg" | "xl";
  position?: "left" | "right" | "top" | "bottom";
  actions?: AmisSchema[];
  closeOnEsc?: boolean;
  closeOnOutside?: boolean;
  showCloseButton?: boolean;
  overlay?: boolean;
  resizable?: boolean;
  className?: string;
  bodyClassName?: string;
  headerClassName?: string;
  data?: Record<string, unknown>;
}

/**
 * 创建 Drawer 抽屉 Schema
 */
export function drawerSchema(opts: DrawerSchemaOptions): AmisSchema {
  return {
    type: "drawer",
    title: opts.title,
    body: opts.body,
    ...(opts.size ? { size: opts.size } : {}),
    ...(opts.position ? { position: opts.position } : {}),
    ...(opts.actions ? { actions: opts.actions } : {}),
    ...(opts.closeOnEsc !== false ? { closeOnEsc: opts.closeOnEsc ?? true } : {}),
    ...(opts.closeOnOutside ? { closeOnOutside: true } : {}),
    ...(opts.showCloseButton !== false ? {} : { showCloseButton: false }),
    ...(opts.overlay !== false ? {} : { overlay: false }),
    ...(opts.resizable ? { resizable: true } : {}),
    ...(opts.className ? { className: opts.className } : {}),
    ...(opts.bodyClassName ? { bodyClassName: opts.bodyClassName } : {}),
    ...(opts.headerClassName ? { headerClassName: opts.headerClassName } : {}),
    ...(opts.data ? { data: opts.data } : {}),
  };
}

// ========== 确认弹窗快捷方式 ==========

/** 确认删除弹窗 */
export function confirmDialog(opts: {
  title?: string;
  body?: string;
  confirmText?: string;
  cancelText?: string;
  onConfirm?: AmisSchema;
}): AmisSchema {
  return dialogSchema({
    title: opts.title ?? "确认操作",
    body: { type: "tpl", tpl: opts.body ?? "确定要执行此操作吗？" },
    size: "sm",
    actions: [
      {
        type: "button",
        label: opts.cancelText ?? "取消",
        actionType: "close",
      },
      {
        type: "button",
        label: opts.confirmText ?? "确定",
        level: "danger",
        actionType: "close",
        ...(opts.onConfirm ? { close: true, ...opts.onConfirm } : {}),
      },
    ],
  });
}

/** 表单弹窗 */
export function formDialog(opts: {
  title: string;
  body: AmisSchema[];
  api: string | Record<string, unknown>;
  size?: "xs" | "sm" | "md" | "lg" | "xl";
  initApi?: string | Record<string, unknown>;
}): AmisSchema {
  return dialogSchema({
    title: opts.title,
    size: opts.size ?? "md",
    body: {
      type: "form",
      api: opts.api,
      ...(opts.initApi ? { initApi: opts.initApi } : {}),
      body: opts.body,
    },
  });
}

/** 表单抽屉 */
export function formDrawer(opts: {
  title: string;
  body: AmisSchema[];
  api: string | Record<string, unknown>;
  size?: "xs" | "sm" | "md" | "lg" | "xl";
  position?: "left" | "right";
  initApi?: string | Record<string, unknown>;
}): AmisSchema {
  return drawerSchema({
    title: opts.title,
    size: opts.size ?? "md",
    position: opts.position ?? "right",
    body: {
      type: "form",
      api: opts.api,
      ...(opts.initApi ? { initApi: opts.initApi } : {}),
      body: opts.body,
    },
  });
}

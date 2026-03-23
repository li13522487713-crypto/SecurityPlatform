/**
 * 【动作 V-5.3 弹窗/刷新/提交】
 * 6 类动作 Schema 片段：dialog / drawer / reload / submit / reset / setValue
 */
import type { AmisSchema } from "@/types/amis";

/** 打开弹窗动作 */
export function dialogAction(opts: {
  label: string;
  dialog: AmisSchema;
  level?: string;
  icon?: string;
  size?: string;
  visibleOn?: string;
  className?: string;
}): AmisSchema {
  return {
    type: "button",
    label: opts.label,
    actionType: "dialog",
    dialog: opts.dialog,
    ...(opts.level ? { level: opts.level } : {}),
    ...(opts.icon ? { icon: opts.icon } : {}),
    ...(opts.size ? { size: opts.size } : {}),
    ...(opts.visibleOn ? { visibleOn: opts.visibleOn } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

/** 打开抽屉动作 */
export function drawerAction(opts: {
  label: string;
  drawer: AmisSchema;
  level?: string;
  icon?: string;
  size?: string;
  visibleOn?: string;
  className?: string;
}): AmisSchema {
  return {
    type: "button",
    label: opts.label,
    actionType: "drawer",
    drawer: opts.drawer,
    ...(opts.level ? { level: opts.level } : {}),
    ...(opts.icon ? { icon: opts.icon } : {}),
    ...(opts.size ? { size: opts.size } : {}),
    ...(opts.visibleOn ? { visibleOn: opts.visibleOn } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

/**
 * 刷新目标组件动作
 *
 * @example
 * ```ts
 * reloadAction({ label: '刷新列表', target: 'userCrud' })
 * // 配合 CRUD 组件使用时，CRUD 需要设置 name: 'userCrud'
 * ```
 */
export function reloadAction(opts: {
  label: string;
  target: string;
  level?: string;
  icon?: string;
  className?: string;
}): AmisSchema {
  return {
    type: "button",
    label: opts.label,
    actionType: "reload",
    target: opts.target,
    ...(opts.level ? { level: opts.level } : {}),
    ...(opts.icon ? { icon: opts.icon } : { icon: "fa fa-refresh" }),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

/** 提交表单动作 */
export function submitAction(opts: {
  label?: string;
  level?: string;
  icon?: string;
  target?: string;
  className?: string;
} = {}): AmisSchema {
  return {
    type: "button",
    label: opts.label ?? "提交",
    actionType: "submit",
    level: opts.level ?? "primary",
    ...(opts.icon ? { icon: opts.icon } : {}),
    ...(opts.target ? { target: opts.target } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

/** 重置表单动作 */
export function resetAction(opts: {
  label?: string;
  level?: string;
  icon?: string;
  target?: string;
  className?: string;
} = {}): AmisSchema {
  return {
    type: "button",
    label: opts.label ?? "重置",
    actionType: "reset",
    ...(opts.level ? { level: opts.level } : {}),
    ...(opts.icon ? { icon: opts.icon } : {}),
    ...(opts.target ? { target: opts.target } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

/**
 * setValue 动作：设置目标组件的值
 *
 * @example
 * ```ts
 * setValueAction({
 *   label: '填充默认值',
 *   target: 'myForm',
 *   value: { name: '默认名称', email: 'default@example.com' },
 * })
 * ```
 */
export function setValueAction(opts: {
  label: string;
  target: string;
  value: Record<string, unknown>;
  level?: string;
  icon?: string;
  className?: string;
}): AmisSchema {
  return {
    type: "button",
    label: opts.label,
    onEvent: {
      click: {
        actions: [{
          actionType: "setValue",
          componentId: opts.target,
          args: { value: opts.value },
        }],
      },
    },
    ...(opts.level ? { level: opts.level } : {}),
    ...(opts.icon ? { icon: opts.icon } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

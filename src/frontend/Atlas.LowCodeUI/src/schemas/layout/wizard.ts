/**
 * 【布局 IV-4.2 向导】
 * Wizard 多步骤表单向导 Builder
 */
import type { AmisSchema } from "@/types/amis";

/** 向导步骤定义 */
export interface WizardStep {
  /** 步骤标题 */
  title: string;
  /** 步骤描述 */
  subTitle?: string;
  /** 步骤表单体 */
  body: AmisSchema[];
  /** 步骤图标 */
  icon?: string;
  /** 步骤完成后提交的 API（可选，最后一步自动使用 wizard 的 api） */
  api?: string | Record<string, unknown>;
  /** 初始化数据 API */
  initApi?: string | Record<string, unknown>;
  /** 该步骤的表单验证模式 */
  mode?: "normal" | "horizontal" | "inline";
  /** 水平布局配置 */
  horizontal?: { left?: number; right?: number };
  /** 进入下一步前的自定义校验表达式 */
  asyncApi?: string | Record<string, unknown>;
}

export interface WizardSchemaOptions {
  /** 步骤数组 */
  steps: WizardStep[];
  /** 最终提交 API */
  api?: string | Record<string, unknown>;
  /** 初始化 API */
  initApi?: string | Record<string, unknown>;
  /** 提交完成后动作 */
  redirect?: string;
  /** 提交完成后重新加载目标 */
  reload?: string;
  /** 是否可跳过步骤 */
  startStep?: number;
  /** 按钮文案 */
  actionPrevLabel?: string;
  actionNextLabel?: string;
  actionFinishLabel?: string;
  /** 是否显示步骤条 */
  showSteps?: boolean;
  /** 步骤条模式 */
  stepsMode?: "horizontal" | "vertical";
  /** 附加 className */
  className?: string;
}

/**
 * 创建 Wizard 多步向导 Schema
 *
 * @example
 * ```ts
 * wizardSchema({
 *   steps: [
 *     { title: '基本信息', body: [inputText({ name: 'name', label: '姓名' })] },
 *     { title: '联系方式', body: [inputEmail({ name: 'email' }), inputText({ name: 'phone', label: '电话' })] },
 *     { title: '确认提交', body: [{ type: 'tpl', tpl: '请确认以上信息' }] },
 *   ],
 *   api: '/api/v1/users',
 * })
 * ```
 */
export function wizardSchema(opts: WizardSchemaOptions): AmisSchema {
  return {
    type: "wizard",
    steps: opts.steps.map((step) => ({
      title: step.title,
      ...(step.subTitle ? { subTitle: step.subTitle } : {}),
      body: step.body,
      ...(step.icon ? { icon: step.icon } : {}),
      ...(step.api ? { api: step.api } : {}),
      ...(step.initApi ? { initApi: step.initApi } : {}),
      ...(step.mode ? { mode: step.mode } : {}),
      ...(step.horizontal ? { horizontal: step.horizontal } : {}),
      ...(step.asyncApi ? { asyncApi: step.asyncApi } : {}),
    })),
    ...(opts.api ? { api: opts.api } : {}),
    ...(opts.initApi ? { initApi: opts.initApi } : {}),
    ...(opts.redirect ? { redirect: opts.redirect } : {}),
    ...(opts.reload ? { reload: opts.reload } : {}),
    ...(opts.startStep ? { startStep: opts.startStep } : {}),
    ...(opts.actionPrevLabel ? { actionPrevLabel: opts.actionPrevLabel } : {}),
    ...(opts.actionNextLabel ? { actionNextLabel: opts.actionNextLabel } : {}),
    ...(opts.actionFinishLabel ? { actionFinishLabel: opts.actionFinishLabel } : {}),
    ...(opts.showSteps !== false ? {} : { showSteps: false }),
    ...(opts.stepsMode ? { stepsMode: opts.stepsMode } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

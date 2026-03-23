/**
 * 【表单 II-2.2 基础控件】
 * 11 类基础表单控件 Schema 工厂函数
 * input-text / password / number / email / url / textarea / editor / input-color / input-rating / input-range / input-tag
 */
import type { AmisSchema } from "@/types/amis";

/** 通用表单控件选项 */
export interface BaseControlOptions {
  name: string;
  label?: string;
  value?: unknown;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  hidden?: boolean;
  description?: string;
  remark?: string;
  validations?: Record<string, unknown>;
  validationErrors?: Record<string, string>;
}

function base(type: string, opts: BaseControlOptions, extra: Record<string, unknown> = {}): AmisSchema {
  return {
    type,
    name: opts.name,
    label: opts.label ?? opts.name,
    ...(opts.value !== undefined ? { value: opts.value } : {}),
    ...(opts.placeholder ? { placeholder: opts.placeholder } : {}),
    ...(opts.required ? { required: true } : {}),
    ...(opts.disabled ? { disabled: true } : {}),
    ...(opts.hidden ? { hidden: true } : {}),
    ...(opts.description ? { description: opts.description } : {}),
    ...(opts.remark ? { remark: opts.remark } : {}),
    ...(opts.validations ? { validations: opts.validations } : {}),
    ...(opts.validationErrors ? { validationErrors: opts.validationErrors } : {}),
    ...extra,
  };
}

/** 单行文本输入框 */
export function inputText(opts: BaseControlOptions & {
  clearable?: boolean;
  addOn?: AmisSchema;
  prefix?: string;
  suffix?: string;
  showCounter?: boolean;
  maxLength?: number;
  minLength?: number;
} = { name: "text" }): AmisSchema {
  return base("input-text", opts, {
    ...(opts.clearable ? { clearable: true } : {}),
    ...(opts.addOn ? { addOn: opts.addOn } : {}),
    ...(opts.prefix ? { prefix: opts.prefix } : {}),
    ...(opts.suffix ? { suffix: opts.suffix } : {}),
    ...(opts.showCounter ? { showCounter: true } : {}),
    ...(opts.maxLength ? { maxLength: opts.maxLength } : {}),
    ...(opts.minLength ? { minLength: opts.minLength } : {}),
  });
}

/** 密码输入框 */
export function inputPassword(opts: BaseControlOptions & {
  revealPassword?: boolean;
} = { name: "password" }): AmisSchema {
  return base("input-password", opts, {
    ...(opts.revealPassword !== false ? { revealPassword: true } : {}),
  });
}

/** 数字输入框 */
export function inputNumber(opts: BaseControlOptions & {
  min?: number;
  max?: number;
  step?: number;
  precision?: number;
  prefix?: string;
  suffix?: string;
  kilobitSeparator?: boolean;
} = { name: "number" }): AmisSchema {
  return base("input-number", opts, {
    ...(opts.min !== undefined ? { min: opts.min } : {}),
    ...(opts.max !== undefined ? { max: opts.max } : {}),
    ...(opts.step ? { step: opts.step } : {}),
    ...(opts.precision !== undefined ? { precision: opts.precision } : {}),
    ...(opts.prefix ? { prefix: opts.prefix } : {}),
    ...(opts.suffix ? { suffix: opts.suffix } : {}),
    ...(opts.kilobitSeparator ? { kilobitSeparator: true } : {}),
  });
}

/** 邮箱输入框 */
export function inputEmail(opts: BaseControlOptions = { name: "email" }): AmisSchema {
  return base("input-email", opts);
}

/** URL 输入框 */
export function inputUrl(opts: BaseControlOptions = { name: "url" }): AmisSchema {
  return base("input-url", opts);
}

/** 多行文本域 */
export function textarea(opts: BaseControlOptions & {
  minRows?: number;
  maxRows?: number;
  showCounter?: boolean;
  maxLength?: number;
} = { name: "textarea" }): AmisSchema {
  return base("textarea", opts, {
    ...(opts.minRows ? { minRows: opts.minRows } : {}),
    ...(opts.maxRows ? { maxRows: opts.maxRows } : {}),
    ...(opts.showCounter ? { showCounter: true } : {}),
    ...(opts.maxLength ? { maxLength: opts.maxLength } : {}),
  });
}

/** 富文本编辑器 */
export function editor(opts: BaseControlOptions & {
  language?: string;
  size?: string;
} = { name: "editor" }): AmisSchema {
  return base("editor", opts, {
    language: opts.language ?? "javascript",
    ...(opts.size ? { size: opts.size } : {}),
  });
}

/** 颜色选择器 */
export function inputColor(opts: BaseControlOptions & {
  format?: "hex" | "rgb" | "rgba" | "hsl";
  presetColors?: string[];
  allowCustomColor?: boolean;
} = { name: "color" }): AmisSchema {
  return base("input-color", opts, {
    ...(opts.format ? { format: opts.format } : {}),
    ...(opts.presetColors ? { presetColors: opts.presetColors } : {}),
    ...(opts.allowCustomColor !== false ? { allowCustomColor: true } : {}),
  });
}

/** 评分 */
export function inputRating(opts: BaseControlOptions & {
  count?: number;
  half?: boolean;
  allowClear?: boolean;
  texts?: Record<number, string>;
  textPosition?: "right" | "left";
} = { name: "rating" }): AmisSchema {
  return base("input-rating", opts, {
    ...(opts.count ? { count: opts.count } : {}),
    ...(opts.half ? { half: true } : {}),
    ...(opts.allowClear ? { allowClear: true } : {}),
    ...(opts.texts ? { texts: opts.texts } : {}),
    ...(opts.textPosition ? { textPosition: opts.textPosition } : {}),
  });
}

/** 滑块 */
export function inputRange(opts: BaseControlOptions & {
  min?: number;
  max?: number;
  step?: number;
  showInput?: boolean;
  multiple?: boolean;
  joinValues?: boolean;
  delimiter?: string;
  unit?: string;
} = { name: "range" }): AmisSchema {
  return base("input-range", opts, {
    ...(opts.min !== undefined ? { min: opts.min } : {}),
    ...(opts.max !== undefined ? { max: opts.max } : {}),
    ...(opts.step ? { step: opts.step } : {}),
    ...(opts.showInput ? { showInput: true } : {}),
    ...(opts.multiple ? { multiple: true } : {}),
    ...(opts.joinValues !== undefined ? { joinValues: opts.joinValues } : {}),
    ...(opts.delimiter ? { delimiter: opts.delimiter } : {}),
    ...(opts.unit ? { unit: opts.unit } : {}),
  });
}

/** 标签输入 */
export function inputTag(opts: BaseControlOptions & {
  options?: Array<{ label: string; value: string }>;
  optionsTip?: string;
  clearable?: boolean;
  max?: number;
  joinValues?: boolean;
  delimiter?: string;
  extractValue?: boolean;
} = { name: "tags" }): AmisSchema {
  return base("input-tag", opts, {
    ...(opts.options ? { options: opts.options } : {}),
    ...(opts.optionsTip ? { optionsTip: opts.optionsTip } : {}),
    ...(opts.clearable ? { clearable: true } : {}),
    ...(opts.max ? { max: opts.max } : {}),
    ...(opts.joinValues !== undefined ? { joinValues: opts.joinValues } : {}),
    ...(opts.delimiter ? { delimiter: opts.delimiter } : {}),
    ...(opts.extractValue ? { extractValue: true } : {}),
  });
}

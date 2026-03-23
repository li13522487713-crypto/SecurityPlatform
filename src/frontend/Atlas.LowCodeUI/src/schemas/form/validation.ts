/**
 * 【表单 II-2.4 验证规则】
 * 表单验证预设工具函数：必填/格式/长度/范围/正则/多字段联合校验
 */

/** 验证规则片段 */
export interface ValidationRule {
  [key: string]: unknown;
}

/** 验证错误消息 */
export interface ValidationMessages {
  [key: string]: string;
}

/** 验证结果：validations + validationErrors */
export interface ValidationConfig {
  validations: ValidationRule;
  validationErrors: ValidationMessages;
}

// ========== 单字段验证 ==========

/** 必填 */
export function required(message = "此字段不能为空"): ValidationConfig {
  return {
    validations: { isRequired: true },
    validationErrors: { isRequired: message },
  };
}

/** 邮箱格式 */
export function isEmail(message = "请输入有效的邮箱地址"): ValidationConfig {
  return {
    validations: { isEmail: true },
    validationErrors: { isEmail: message },
  };
}

/** URL 格式 */
export function isUrl(message = "请输入有效的 URL"): ValidationConfig {
  return {
    validations: { isUrl: true },
    validationErrors: { isUrl: message },
  };
}

/** 数字格式 */
export function isNumeric(message = "请输入数字"): ValidationConfig {
  return {
    validations: { isNumeric: true },
    validationErrors: { isNumeric: message },
  };
}

/** 整数格式 */
export function isInt(message = "请输入整数"): ValidationConfig {
  return {
    validations: { isInt: true },
    validationErrors: { isInt: message },
  };
}

/** IP 地址格式 */
export function isIp(message = "请输入有效的 IP 地址"): ValidationConfig {
  return {
    validations: { matchRegexp: "/^(\\d{1,3}\\.){3}\\d{1,3}$/" },
    validationErrors: { matchRegexp: message },
  };
}

/** 中国手机号 */
export function isPhoneNumber(message = "请输入有效的手机号码"): ValidationConfig {
  return {
    validations: { matchRegexp: "/^1[3-9]\\d{9}$/" },
    validationErrors: { matchRegexp: message },
  };
}

/** 正则匹配 */
export function matchRegexp(pattern: string, message = "格式不正确"): ValidationConfig {
  return {
    validations: { matchRegexp: pattern },
    validationErrors: { matchRegexp: message },
  };
}

/** 最小长度 */
export function minLength(length: number, message?: string): ValidationConfig {
  return {
    validations: { minLength: length },
    validationErrors: { minLength: message ?? `长度不能小于 ${length}` },
  };
}

/** 最大长度 */
export function maxLength(length: number, message?: string): ValidationConfig {
  return {
    validations: { maxLength: length },
    validationErrors: { maxLength: message ?? `长度不能大于 ${length}` },
  };
}

/** 最小值 */
export function minimum(value: number, message?: string): ValidationConfig {
  return {
    validations: { minimum: value },
    validationErrors: { minimum: message ?? `值不能小于 ${value}` },
  };
}

/** 最大值 */
export function maximum(value: number, message?: string): ValidationConfig {
  return {
    validations: { maximum: value },
    validationErrors: { maximum: message ?? `值不能大于 ${value}` },
  };
}

/** 长度区间 */
export function lengthRange(min: number, max: number, message?: string): ValidationConfig {
  return {
    validations: { minLength: min, maxLength: max },
    validationErrors: {
      minLength: message ?? `长度不能小于 ${min}`,
      maxLength: message ?? `长度不能大于 ${max}`,
    },
  };
}

/** 数值区间 */
export function numberRange(min: number, max: number, message?: string): ValidationConfig {
  return {
    validations: { minimum: min, maximum: max },
    validationErrors: {
      minimum: message ?? `值不能小于 ${min}`,
      maximum: message ?? `值不能大于 ${max}`,
    },
  };
}

// ========== 组合验证 ==========

/**
 * 合并多个验证规则
 *
 * @example
 * ```ts
 * const rules = mergeValidations(
 *   required(),
 *   minLength(3),
 *   maxLength(50),
 *   isEmail(),
 * );
 * // { validations: { isRequired: true, minLength: 3, maxLength: 50, isEmail: true }, validationErrors: { ... } }
 * ```
 */
export function mergeValidations(...configs: ValidationConfig[]): ValidationConfig {
  const merged: ValidationConfig = {
    validations: {},
    validationErrors: {},
  };

  for (const config of configs) {
    Object.assign(merged.validations, config.validations);
    Object.assign(merged.validationErrors, config.validationErrors);
  }

  return merged;
}

// ========== 多字段联合校验 ==========

/**
 * 创建 AMIS rules 联合校验规则
 *
 * @description
 * AMIS 支持通过 `rules` 属性定义自定义校验规则。
 * 与 validations 不同，rules 是独立的校验规则数组，每条规则包含 rule 表达式和 message。
 *
 * @example
 * ```ts
 * // 确保 endDate > startDate
 * customRules([
 *   { rule: "data.endDate > data.startDate", message: "结束日期必须晚于开始日期" },
 * ])
 * ```
 */
export function customRules(rules: Array<{ rule: string; message: string }>): Record<string, unknown> {
  return { rules };
}

// ========== 预设组合 ==========

/** 用户名验证（必填 + 3~50字符） */
export function usernameValidation(): ValidationConfig {
  return mergeValidations(required("用户名不能为空"), lengthRange(3, 50));
}

/** 密码验证（必填 + 8~128字符） */
export function passwordValidation(): ValidationConfig {
  return mergeValidations(required("密码不能为空"), lengthRange(8, 128));
}

/** 邮箱验证（必填 + 邮箱格式） */
export function emailValidation(): ValidationConfig {
  return mergeValidations(required("邮箱不能为空"), isEmail());
}

/** 手机号验证（必填 + 手机格式） */
export function phoneValidation(): ValidationConfig {
  return mergeValidations(required("手机号不能为空"), isPhoneNumber());
}

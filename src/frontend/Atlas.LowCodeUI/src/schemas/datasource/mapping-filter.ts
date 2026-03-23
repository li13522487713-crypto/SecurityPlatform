/**
 * 【数据源 VI-6.3 数据映射/过滤器】
 * AMIS 数据映射语法和常用过滤器示例集
 */

/**
 * AMIS 数据映射语法说明
 *
 * AMIS 使用 `${}` 语法进行数据映射：
 * - `${var}` — 简单变量引用
 * - `${obj.field}` — 嵌套对象字段
 * - `${arr[0]}` — 数组索引
 * - `${val|filter}` — 过滤器
 * - `${val|filter:arg}` — 带参数的过滤器
 * - `${val|filter1|filter2}` — 过滤器链
 */

// ========== 数据映射工具 ==========

/** 简单变量引用 */
export function ref(field: string): string {
  return `\${${field}}`;
}

/** 嵌套对象字段引用 */
export function nestedRef(path: string): string {
  return `\${${path}}`;
}

/** 数组索引引用 */
export function arrayRef(field: string, index: number): string {
  return `\${${field}[${index}]}`;
}

// ========== 过滤器工具 ==========

/** 应用过滤器 */
export function filter(field: string, filterName: string, ...args: Array<string | number>): string {
  const argStr = args.length > 0 ? `:${args.join(",")}` : "";
  return `\${${field}|${filterName}${argStr}}`;
}

/** 大写 */
export function upperCase(field: string): string {
  return filter(field, "upperCase");
}

/** 小写 */
export function lowerCase(field: string): string {
  return filter(field, "lowerCase");
}

/** 首字母大写 */
export function capitalize(field: string): string {
  return filter(field, "capitalize");
}

/** 日期格式化 */
export function dateFormat(field: string, format = "YYYY-MM-DD"): string {
  return `\${${field}|date:${format}}`;
}

/** 日期时间格式化 */
export function datetimeFormat(field: string, format = "YYYY-MM-DD HH:mm:ss"): string {
  return `\${${field}|date:${format}}`;
}

/** 数字格式化（保留小数位） */
export function numberFormat(field: string, decimals = 2): string {
  return `\${${field}|number:${decimals}}`;
}

/** 货币格式化（千分位分隔） */
export function currencyFormat(field: string, decimals = 2, symbol = "¥"): string {
  return `${symbol}\${${field}|number:${decimals}}`;
}

/** 百分比格式化 */
export function percentFormat(field: string, decimals = 1): string {
  return `\${${field}|number:${decimals}}%`;
}

/** 截断文本 */
export function truncate(field: string, length: number, ellipsis = "..."): string {
  return `\${${field}|truncate:${length}:${ellipsis}}`;
}

/** 默认值（当字段为空时使用指定值） */
export function defaultValue(field: string, fallback: string): string {
  return `\${${field}|default:${fallback}}`;
}

/** HTML 转义 */
export function escape(field: string): string {
  return filter(field, "escape");
}

/** JSON 字符串化 */
export function jsonStringify(field: string): string {
  return filter(field, "json");
}

/** 数组 join */
export function join(field: string, separator = ","): string {
  return `\${${field}|join:${separator}}`;
}

/** 数组 first */
export function first(field: string): string {
  return filter(field, "first");
}

/** 数组 last */
export function last(field: string): string {
  return filter(field, "last");
}

/** 数组 count */
export function count(field: string): string {
  return filter(field, "count");
}

/** 数组 sum */
export function sum(field: string): string {
  return filter(field, "sum");
}

/** 文件大小格式化 */
export function fileSizeFormat(field: string): string {
  return filter(field, "bytes");
}

// ========== 常用模板片段 ==========

/** 创建日期范围展示模板 */
export function dateRangeTpl(startField: string, endField: string, format = "YYYY-MM-DD"): string {
  return `\${${startField}|date:${format}} ~ \${${endField}|date:${format}}`;
}

/** 创建用户名展示模板（名称 + 邮箱） */
export function userTpl(nameField = "name", emailField = "email"): string {
  return `\${${nameField}} (\${${emailField}})`;
}

/** 创建状态标签模板 */
export function statusTpl(field: string, map: Record<string, { label: string; color: string }>): string {
  const entries = Object.entries(map);
  const conditions = entries
    .map(([value, { label, color }]) => `\${${field} === '${value}' ? '<span style="color:${color}">${label}</span>' : ''}`)
    .join("");
  return conditions || `\${${field}}`;
}

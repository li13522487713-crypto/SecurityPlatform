/**
 * 安全的 JavaScript 表达式沙盒执行引擎
 * 拦截对全局对象（如 window、document）的访问，防止恶意代码执行。
 */

// 白名单全局对象/函数
const ALLOWED_GLOBALS = new Set([
  'Math', 'Date', 'String', 'Number', 'Boolean', 'Array', 'Object', 'JSON',
  'parseInt', 'parseFloat', 'isNaN', 'isFinite', 'decodeURI', 'encodeURI',
  'undefined', 'null', 'NaN', 'Infinity'
]);

/**
 * 在沙盒环境中执行表达式并返回结果
 * @param expr JS 表达式字符串
 * @param context 当前作用域变量上下文
 */
export function evaluateExpression(expr: string, context: Record<string, any> = {}): any {
  if (!expr || typeof expr !== 'string') return expr;
  
  // 构建 Proxy 拦截对全局作用域和受控变量的非预期访问
  const sandboxContext = new Proxy(context, {
    has(target, key) {
      if (typeof key === 'symbol') return Reflect.has(target, key);
      return true; // 欺骗 with()，让所有的变量查找都落到 proxy 的 get
    },
    get(target, key, receiver) {
      if (key === Symbol.unscopables) return undefined;
      
      // 如果上下文中包含这个变量，则返回
      if (Reflect.has(target, key)) {
        return Reflect.get(target, key, receiver);
      }
      
      // 如果是白名单的全局变量，允许访问 window 上的安全对象
      if (typeof key === 'string' && ALLOWED_GLOBALS.has(key)) {
        return (window as any)[key];
      }
      
      // 阻止非法访问如 window, document 等
      if (key === 'window' || key === 'document' || key === 'globalThis') {
        throw new Error(`[Sandbox] Access denied to global object: ${String(key)}`);
      }
      
      return undefined;
    }
  });

  try {
    // 使用 with() 将 proxy 挂载为顶层作用域，通过 new Function 执行
    const fn = new Function('sandboxContext', `
      with (sandboxContext) {
        return (${expr});
      }
    `);
    
    return fn(sandboxContext);
  } catch (err: any) {
    console.warn(`[ExpressionEngine] Failed to evaluate expression: ${expr}`, err);
    return undefined; // 或者根据需求看是否要 throw
  }
}

/**
 * 解析包含 {{ variables }} 的 Mustache 格式字符串。
 * 如果整个字符串只是一个表达式 `{{ expr }}`，则返回该表达式的求值结果（保持类型）。
 * 如果是混合文本 `Hello {{ name }}`，则返回拼接后的字符串。
 * @param text 待解析文本
 * @param context 变量上下文
 */
export function parseMustache(text: string, context: Record<string, any> = {}): any {
  if (typeof text !== 'string') return text;
  
  // 匹配 {{ expr }} 格式
  const regex = /\{\{\s*(.+?)\s*\}\}/g;
  
  // 如果整个字符串就是单一变量表达式，直接计算并保持原本类型
  const exactMatch = text.match(/^\{\{\s*(.+?)\s*\}\}$/);
  if (exactMatch) {
    return evaluateExpression(exactMatch[1], context);
  }
  
  // 否则作为模板字符串，进行内部替换
  return text.replace(regex, (match, expr) => {
    const val = evaluateExpression(expr, context);
    return val !== undefined && val !== null ? String(val) : '';
  });
}

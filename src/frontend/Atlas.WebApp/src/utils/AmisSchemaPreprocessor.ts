import { parseMustache } from './ExpressionEngine';

/**
 * AMIS Schema 的预处理引擎，注入变量沙盒能力。
 * 对配置好的 $vars 或 带有 {{}} 的属性进行求值。
 */
export class AmisSchemaPreprocessor {
  /**
   * 递归遍历 Schema 树并处理里面的字符串值
   * @param schema AMIS 原始配置 (由于可能被改变，注意是否传递深拷贝)
   * @param context 外部注入的页面级变量上下文
   */
  static process(schema: any, context: Record<string, any> = {}): any {
    if (!schema) return schema;

    // 基础类型处理
    if (typeof schema === 'string') {
      return parseMustache(schema, context);
    }

    if (Array.isArray(schema)) {
      return schema.map(item => this.process(item, context));
    }

    if (typeof schema === 'object') {
      const result: Record<string, any> = {};
      
      // 合并当前层次声明的变量作用域 (针对如果在 schema 中定义了局部 $vars)
      let currentContext = { ...context };
      if (schema.$vars && typeof schema.$vars === 'object') {
        currentContext = { ...currentContext, ...schema.$vars };
        delete schema.$vars; // 从最终渲染的 schema 中去掉不需要的自定义声明
      }

      for (const [key, value] of Object.entries(schema)) {
        result[key] = this.process(value, currentContext);
      }
      return result;
    }

    return schema;
  }
}

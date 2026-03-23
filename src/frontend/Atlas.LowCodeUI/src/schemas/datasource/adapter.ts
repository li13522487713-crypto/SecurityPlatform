/**
 * 【数据源 VI-6.4 接口适配】
 * ResponseAdapterBuilder：将非标准接口 responseData 映射为 AMIS 规范格式
 */

/**
 * AMIS 标准接口响应格式
 *
 * ```json
 * {
 *   "status": 0,       // 0 表示成功
 *   "msg": "",
 *   "data": {
 *     "items": [...],  // 列表数据
 *     "total": 100     // 总条数
 *   }
 * }
 * ```
 *
 * 当后端接口返回非标准格式时，需要通过 adaptor 或 responseData 适配。
 */

/**
 * 创建 responseData 映射（声明式适配）
 *
 * @description
 * AMIS 支持通过 responseData 属性直接映射响应字段，无需写 JS 代码。
 *
 * @example
 * ```ts
 * // 后端返回: { data: { records: [...], totalCount: 100 } }
 * // 需要映射为: { items: [...], total: 100 }
 * responseDataMapping({ items: '${records}', total: '${totalCount}' })
 * ```
 */
export function responseDataMapping(mapping: Record<string, string>): Record<string, unknown> {
  return { responseData: mapping };
}

/**
 * 创建 adaptor 适配器（编程式适配）
 *
 * @description
 * adaptor 是一段 JS 函数字符串，接收 (payload, response, api) 三个参数。
 * 必须返回 AMIS 标准格式 { status, msg, data }。
 *
 * @example
 * ```ts
 * // Atlas API 标准格式适配
 * atlasApiAdaptor()
 *
 * // 自定义适配
 * customAdaptor(`
 *   return {
 *     status: payload.code === 'OK' ? 0 : 1,
 *     msg: payload.message,
 *     data: payload.result,
 *   };
 * `)
 * ```
 */
export function customAdaptor(script: string): Record<string, string> {
  return { adaptor: script };
}

/**
 * Atlas 平台标准 API 响应适配器
 *
 * @description
 * 将 Atlas ApiResponse<T> 格式适配为 AMIS 标准格式：
 * - Atlas: { success, code, message, data, traceId }
 * - AMIS:  { status, msg, data }
 */
export function atlasApiAdaptor(): Record<string, string> {
  return customAdaptor(`
    return {
      status: payload.success !== false ? 0 : 1,
      msg: payload.message || '',
      data: payload.data,
    };
  `);
}

/**
 * Atlas 分页接口适配器
 *
 * @description
 * 将 Atlas PagedResult<T> 格式适配为 AMIS CRUD 分页格式：
 * - Atlas: { success, data: { items, totalCount, pageIndex, pageSize } }
 * - AMIS:  { status, data: { items, total, page } }
 */
export function atlasPagedAdaptor(): Record<string, string> {
  return customAdaptor(`
    const d = payload.data || {};
    return {
      status: payload.success !== false ? 0 : 1,
      msg: payload.message || '',
      data: {
        items: d.items || [],
        total: d.totalCount || 0,
        page: d.pageIndex || 1,
      },
    };
  `);
}

/**
 * 创建 requestAdaptor 请求适配器
 *
 * @description
 * requestAdaptor 在请求发出前执行，可以修改请求参数。
 *
 * @example
 * ```ts
 * requestAdaptor(`
 *   api.data.pageIndex = api.data.page;
 *   api.data.pageSize = api.data.perPage;
 *   delete api.data.page;
 *   delete api.data.perPage;
 *   return api;
 * `)
 * ```
 */
export function requestAdaptor(script: string): Record<string, string> {
  return { requestAdaptor: script };
}

/**
 * Atlas CRUD 请求适配器
 *
 * @description
 * 将 AMIS 默认分页参数 (page/perPage) 适配为 Atlas API 格式 (PageIndex/PageSize)
 */
export function atlasCrudRequestAdaptor(): Record<string, string> {
  return requestAdaptor(`
    api.data.PageIndex = api.data.page || 1;
    api.data.PageSize = api.data.perPage || 20;
    if (api.data.orderBy) {
      api.data.OrderBy = api.data.orderBy;
      api.data.OrderDir = api.data.orderDir || 'asc';
    }
    delete api.data.page;
    delete api.data.perPage;
    delete api.data.orderBy;
    delete api.data.orderDir;
    return api;
  `);
}

/**
 * 组合 request + response 适配器
 */
export function atlasFullAdaptor(): Record<string, string> {
  return {
    ...atlasCrudRequestAdaptor(),
    ...atlasPagedAdaptor(),
  };
}

import DeveloperApiService from "../../../../../packages/arch/bot-api/src/idl/developer_api";
import { axiosInstance, type BotAPIRequestConfig } from "../../../../../packages/arch/bot-api/src/axios";

const DEVELOPER_GATEWAY_PREFIX = "/api/app-web/coze-developer";

function rewriteDeveloperUrl(url?: string): string | undefined {
  if (!url) {
    return url;
  }

  const mappings: Array<[string, string]> = [
    ["/api/bot", DEVELOPER_GATEWAY_PREFIX],
    ["/api/developer", DEVELOPER_GATEWAY_PREFIX],
    ["/api/draftbot", DEVELOPER_GATEWAY_PREFIX],
    ["/api/space", DEVELOPER_GATEWAY_PREFIX],
    ["/api/workflow", DEVELOPER_GATEWAY_PREFIX],
    ["/api/workflowV2", DEVELOPER_GATEWAY_PREFIX],
    ["/api/conversation", DEVELOPER_GATEWAY_PREFIX],
    ["/api/connector", DEVELOPER_GATEWAY_PREFIX],
    ["/api/connector_user", DEVELOPER_GATEWAY_PREFIX],
    ["/api/card", DEVELOPER_GATEWAY_PREFIX],
    ["/api/acrosite", DEVELOPER_GATEWAY_PREFIX],
    ["/api/task", DEVELOPER_GATEWAY_PREFIX],
  ];

  for (const [from, to] of mappings) {
    if (url === from) {
      return to;
    }
    if (url.startsWith(`${from}/`)) {
      return `${to}${url.slice(from.length)}`;
    }
  }

  return url;
}

export const DeveloperApi = new DeveloperApiService<BotAPIRequestConfig>({
  request: (params, config = {}) =>
    axiosInstance.request({
      ...params,
      ...config,
      url: rewriteDeveloperUrl(params.url),
    }),
});

export * from "../../../../../packages/arch/bot-api/src/idl/developer_api";
export { default } from "../../../../../packages/arch/bot-api/src/idl/developer_api";

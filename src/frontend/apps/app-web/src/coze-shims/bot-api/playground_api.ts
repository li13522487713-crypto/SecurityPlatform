import PlaygroundApiService from "../../../../../packages/arch/bot-api/src/idl/playground_api";
import { axiosInstance, type BotAPIRequestConfig } from "../../../../../packages/arch/bot-api/src/axios";

const PLAYGROUND_GATEWAY_PREFIX = "/api/app-web/coze-playground";
const PLAYGROUND_OPEN_PREFIX = `${PLAYGROUND_GATEWAY_PREFIX}/open`;

function rewritePlaygroundUrl(url?: string): string | undefined {
  if (!url) {
    return url;
  }

  const exactMappings: Array<[string, string]> = [
    ["/api/marketplace/product/favorite/list.v2", `${PLAYGROUND_GATEWAY_PREFIX}/marketplace/product/favorite/list.v2`],
    ["/api/marketplace/product/favorite/list", `${PLAYGROUND_GATEWAY_PREFIX}/marketplace/product/favorite/list`],
  ];

  for (const [from, to] of exactMappings) {
    if (url === from) {
      return to;
    }
  }

  const prefixMappings: Array<[string, string]> = [
    ["/api/playground_api", PLAYGROUND_GATEWAY_PREFIX],
    ["/v1/workspaces", `${PLAYGROUND_OPEN_PREFIX}/workspaces`],
    ["/api/v1/workspaces", `${PLAYGROUND_OPEN_PREFIX}/workspaces`],
  ];

  for (const [from, to] of prefixMappings) {
    if (url === from) {
      return to;
    }
    if (url.startsWith(`${from}/`)) {
      return `${to}${url.slice(from.length)}`;
    }
  }

  return url;
}

export const PlaygroundApi = new PlaygroundApiService<BotAPIRequestConfig>({
  request: (params, config = {}) => {
    const headers = Object.assign({}, config.headers || {}, {
      "Agw-Js-Conv": "str",
    });

    return axiosInstance.request({
      ...params,
      ...config,
      url: rewritePlaygroundUrl(params.url),
      headers,
    });
  },
});

export * from "../../../../../packages/arch/bot-api/src/idl/playground_api";
export { default } from "../../../../../packages/arch/bot-api/src/idl/playground_api";

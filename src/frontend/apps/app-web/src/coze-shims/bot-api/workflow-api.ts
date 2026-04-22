import WorkflowApiService from "../../../../../packages/arch/bot-api/src/idl/workflow_api";
import { axiosInstance, type BotAPIRequestConfig } from "../../../../../packages/arch/bot-api/src/axios";

const WORKFLOW_GATEWAY_PREFIX = "/api/app-web/workflow-sdk";

function rewriteWorkflowUrl(url?: string): string | undefined {
  if (!url) {
    return url;
  }

  const mappings: Array<[string, string]> = [
    ["/api/workflow_api", WORKFLOW_GATEWAY_PREFIX],
    ["/api/op_workflow", WORKFLOW_GATEWAY_PREFIX],
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

export const workflowApi = new WorkflowApiService<BotAPIRequestConfig>({
  request: (params, config = {}) => {
    const headers = Object.assign({}, config.headers || {}, {
      "Agw-Js-Conv": "str",
    });

    return axiosInstance.request({
      ...params,
      ...config,
      url: rewriteWorkflowUrl(params.url),
      headers,
    });
  },
});

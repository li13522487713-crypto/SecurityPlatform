// External Collaboration Connectors API client adapter（v4 报告 27-31 章）。
// 把 app-web 的 requestApi 包装为 @atlas/external-connectors-react 期望的 ConnectorHttpClient。

import { createConnectorApi, type ConnectorApi, type ConnectorHttpClient } from "@atlas/external-connectors-react";
import { requestApi } from "./api-core";

function mergeQuery(url: string, query?: Record<string, unknown>): string {
  if (!query) return url;
  const qs = new URLSearchParams();
  for (const [key, raw] of Object.entries(query)) {
    if (raw === undefined || raw === null) continue;
    qs.append(key, String(raw));
  }
  const serialized = qs.toString();
  if (!serialized) return url;
  return url.includes("?") ? `${url}&${serialized}` : `${url}?${serialized}`;
}

const httpClient: ConnectorHttpClient = {
  get<T>(url: string, query?: Record<string, unknown>): Promise<T> {
    return requestApi<T>(mergeQuery(url, query), { method: "GET" });
  },
  post<T>(url: string, body?: unknown): Promise<T> {
    return requestApi<T>(url, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body), headers: { "content-type": "application/json" } });
  },
  put<T>(url: string, body?: unknown): Promise<T> {
    return requestApi<T>(url, { method: "PUT", body: body === undefined ? undefined : JSON.stringify(body), headers: { "content-type": "application/json" } });
  },
  patch<T>(url: string, body?: unknown): Promise<T> {
    return requestApi<T>(url, { method: "PATCH", body: body === undefined ? undefined : JSON.stringify(body), headers: { "content-type": "application/json" } });
  },
  delete<T>(url: string): Promise<T> {
    return requestApi<T>(url, { method: "DELETE" });
  },
};

export const connectorApi: ConnectorApi = createConnectorApi(httpClient);

export function getConnectorApi(): ConnectorApi {
  return connectorApi;
}

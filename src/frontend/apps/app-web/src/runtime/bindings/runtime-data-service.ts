import type { ApiResponse } from "@atlas/shared-core";
import { requestApi, buildRuntimeRecordsUrl } from "@/services/api-runtime";
import type { DataBinding } from "./binding-types";
import { buildQueryFromBinding as buildQueryFromBindingCore } from "@atlas/runtime-core";
import {
  queryRuntimeRecords as queryRuntimeRecordsCore,
  getRuntimeRecord as getRuntimeRecordCore,
  queryEntityRecords as queryEntityRecordsCore,
  getEntityRecord as getEntityRecordCore,
  createEntityRecord as createEntityRecordCore,
  updateEntityRecord as updateEntityRecordCore,
  type RuntimeDataClient,
  type RuntimeDataQueryParams,
  type EntityDataQueryParams,
} from "@atlas/runtime-core";

function createRuntimeDataClient(): RuntimeDataClient {
  return {
    buildRuntimeRecordsUrl,
    request: async <T>(url: string, init?: { method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE"; body?: unknown }) => {
      const response = await requestApi<ApiResponse<T>>(
        url,
        {
          method: (init?.method as string) ?? "GET",
          body: init?.body === undefined ? undefined : JSON.stringify(init.body),
        },
      );
      return response.data as T;
    },
  };
}

export async function queryRuntimeRecords(params: RuntimeDataQueryParams): Promise<unknown> {
  return queryRuntimeRecordsCore(params, createRuntimeDataClient());
}

export async function getRuntimeRecord(
  pageKey: string,
  appKey: string,
  recordId: string,
): Promise<unknown> {
  return getRuntimeRecordCore(pageKey, appKey, recordId, createRuntimeDataClient());
}

export async function queryEntityRecords(params: EntityDataQueryParams): Promise<unknown> {
  return queryEntityRecordsCore(params, createRuntimeDataClient());
}

export async function getEntityRecord(
  tableKey: string,
  appKey: string,
  recordId: string | number,
): Promise<unknown> {
  return getEntityRecordCore(tableKey, appKey, recordId, createRuntimeDataClient());
}

export async function createEntityRecord(
  tableKey: string,
  appKey: string,
  data: Record<string, unknown>,
): Promise<unknown> {
  return createEntityRecordCore(tableKey, appKey, data, createRuntimeDataClient());
}

export async function updateEntityRecord(
  tableKey: string,
  appKey: string,
  recordId: string | number,
  data: Record<string, unknown>,
): Promise<unknown> {
  return updateEntityRecordCore(tableKey, appKey, recordId, data, createRuntimeDataClient());
}

export { buildQueryFromBindingCore as buildQueryFromBinding };
export type { DataBinding };

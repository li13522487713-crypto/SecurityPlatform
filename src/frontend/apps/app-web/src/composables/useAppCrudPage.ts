import { computed, type ComputedRef, type Ref } from "vue";
import { useCrudPage, type CrudPermissions, type UseCrudPageOptions, type PagedRequest } from "@atlas/shared-core";
import type { Rule } from "ant-design-vue/es/form";
import { useAppContext } from "./useAppContext";
import { tableViewApi } from "@/services/api-table-views";

type AppScopedApi<TList, TDetail, TCreate extends object, TUpdate extends object, TListParams extends object> = {
  list: (appId: string, params: TListParams) => Promise<{ items: TList[]; total: number; pageIndex: number; pageSize: number }>;
  detail?: (appId: string, id: string) => Promise<TDetail>;
  create: (appId: string, data: TCreate) => Promise<unknown>;
  update: (appId: string, id: string, data: TUpdate) => Promise<unknown>;
  delete?: (appId: string, id: string) => Promise<unknown>;
};

interface UseAppCrudPageOptions<TList, TDetail, TCreate extends object, TUpdate extends object, TListParams extends object>
  extends Omit<
    UseCrudPageOptions<TList, TDetail, TCreate, TUpdate, TListParams>,
    "api" | "permissions" | "tableViewApi" | "formRules"
  > {
  permissions: CrudPermissions;
  appApi: AppScopedApi<TList, TDetail, TCreate, TUpdate, TListParams>;
  formRules: Record<string, Rule[]>;
}

export function useAppCrudPage<
  TList,
  TDetail,
  TCreate extends object,
  TUpdate extends object,
  TListParams extends object = PagedRequest
>(
  options: UseAppCrudPageOptions<TList, TDetail, TCreate, TUpdate, TListParams>
) {
  const { appId } = useAppContext();
  const getAppId = (): string => {
    if (!appId.value) {
      throw new Error("App context is not ready.");
    }
    return appId.value;
  };

  const wrappedApi = {
    list: (params: TListParams) => options.appApi.list(getAppId(), params),
    detail: options.appApi.detail
      ? (id: string) => options.appApi.detail!(getAppId(), id)
      : undefined,
    create: (data: TCreate) => options.appApi.create(getAppId(), data),
    update: (id: string, data: TUpdate) => options.appApi.update(getAppId(), id, data),
    delete: options.appApi.delete
      ? (id: string) => options.appApi.delete!(getAppId(), id)
      : undefined
  };

  return useCrudPage<TList, TDetail, TCreate, TUpdate, TListParams>({
    ...options,
    api: wrappedApi,
    tableViewApi
  });
}

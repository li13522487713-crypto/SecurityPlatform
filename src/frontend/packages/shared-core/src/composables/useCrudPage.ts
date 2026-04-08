import { onMounted, onUnmounted, reactive, ref } from "vue";
import type { ComputedRef, Ref } from "vue";
import type { TablePaginationConfig, FormInstance } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import { useTableView } from "./useTableView";
import type {
  TableViewColumn,
  TableViewApiFunctions,
  TranslateFn,
} from "./useTableView";
import type { PagedRequest, PagedResult } from "../types/api-base";
import type { AuthProfile } from "../types/api-base";
import { getAuthProfile, hasPermission } from "../utils/auth";
import type { FormMode } from "../utils/common";
import type { AdvancedQueryConfig } from "../types/advanced-query";

export interface CrudApi<
  TList,
  TDetail,
  TCreate extends object,
  TUpdate extends object,
  TListParams extends object = PagedRequest,
> {
  list: (params: TListParams) => Promise<PagedResult<TList>>;
  detail?: (id: string) => Promise<TDetail>;
  create: (data: TCreate) => Promise<unknown>;
  update: (id: string, data: TUpdate) => Promise<unknown>;
  delete?: (id: string) => Promise<unknown>;
}

export interface CrudPermissions {
  create?: string;
  update?: string;
  delete?: string;
  [key: string]: string | undefined;
}

export interface UseCrudPageOptions<
  TList,
  TDetail,
  TCreate extends object,
  TUpdate extends object,
  TListParams extends object = PagedRequest,
> {
  tableKey: string;
  columns:
    | TableViewColumn<TList>[]
    | Ref<TableViewColumn<TList>[]>
    | ComputedRef<TableViewColumn<TList>[]>;
  permissions: CrudPermissions;
  api: CrudApi<TList, TDetail, TCreate, TUpdate, TListParams>;
  defaultFormModel: () => TCreate & TUpdate;
  formRules: Record<string, Rule[]>;
  formRef?: ReturnType<typeof ref<FormInstance>>;
  buildListParams?: (
    base: PagedRequest,
    advancedQuery: AdvancedQueryConfig
  ) => TListParams;
  buildCreatePayload?: (model: TCreate & TUpdate) => TCreate;
  buildUpdatePayload?: (model: TCreate & TUpdate) => TUpdate;
  mapDetailToForm?: (detail: TDetail, model: TCreate & TUpdate) => void;
  mapRecordToForm?: (record: TList, model: TCreate & TUpdate) => void;
  onAfterSubmit?: () => void | Promise<void>;
  onAfterDelete?: () => void | Promise<void>;
  autoFetch?: boolean;
  defaultQueryConfig?: () => AdvancedQueryConfig;
  tableViewApi: TableViewApiFunctions;
  translate?: TranslateFn;
}

const defaultTranslate: TranslateFn = (key: string, params?: Record<string, unknown>) => {
  const map: Record<string, string> = {
    "crud.totalItems": `Total ${params?.total ?? 0} items`,
    "crud.queryFailed": "Query failed",
    "crud.createSuccess": "Created successfully",
    "crud.updateSuccess": "Updated successfully",
    "crud.submitFailed": "Submit failed",
    "crud.deleteSuccess": "Deleted successfully",
    "crud.deleteFailed": "Delete failed",
    "crud.detailByIdNotSupported": "Detail by ID not supported",
    "crud.loadDetailFailed": "Failed to load detail",
  };
  return map[key] ?? key;
};

export function useCrudPage<
  TList,
  TDetail,
  TCreate extends object,
  TUpdate extends object,
  TListParams extends object = PagedRequest,
>(
  options: UseCrudPageOptions<TList, TDetail, TCreate, TUpdate, TListParams>
) {
  const {
    tableKey,
    columns,
    permissions,
    api,
    defaultFormModel,
    formRules,
    buildListParams,
    buildCreatePayload,
    buildUpdatePayload,
    mapDetailToForm,
    mapRecordToForm,
    onAfterSubmit,
    onAfterDelete,
    autoFetch = true,
    defaultQueryConfig = () => ({
      rootGroup: { id: "root", conjunction: "and" as const, rules: [], groups: [] },
    }),
    tableViewApi,
  } = options;

  const t = options.translate ?? defaultTranslate;

  const dataSource = ref<TList[]>([]) as { value: TList[] };
  const loading = ref(false);
  const keyword = ref("");
  const pagination = reactive<TablePaginationConfig>({
    current: 1,
    pageSize: 20,
    total: 0,
    showTotal: (total: number) => t("crud.totalItems", { total }),
  });

  const advancedQueryConfig = ref<AdvancedQueryConfig>(defaultQueryConfig());

  const formVisible = ref(false);
  const formMode = ref<FormMode>("create");
  const formRef = options.formRef ?? ref<FormInstance>();
  const formModel = reactive<TCreate & TUpdate>(
    defaultFormModel()
  ) as TCreate & TUpdate;
  const selectedId = ref<string | null>(null);
  const submitting = ref(false);

  const profile: AuthProfile | null = getAuthProfile();
  const permissionMap = {} as Record<string, boolean>;
  for (const [key, code] of Object.entries(permissions)) {
    if (code) {
      permissionMap[key] = hasPermission(profile, code);
    }
  }
  const canCreate = permissionMap.create ?? false;
  const canUpdate = permissionMap.update ?? false;
  const canDelete = permissionMap.delete ?? false;

  const hasPermissionFor = (key: string): boolean => {
    return permissionMap[key] ?? false;
  };

  const isMounted = ref(false);

  const fetchData = async () => {
    loading.value = true;
    try {
      const baseParams: PagedRequest = {
        pageIndex: pagination.current ?? 1,
        pageSize: pagination.pageSize ?? 20,
        keyword: keyword.value || undefined,
      };
      const params = buildListParams
        ? buildListParams(baseParams, advancedQueryConfig.value)
        : (baseParams as unknown as TListParams);
      const result = await api.list(params);

      if (!isMounted.value) return;

      dataSource.value = result.items;
      pagination.total = Number(result.total) || 0;
    } catch (error) {
      if (!isMounted.value) return;
      message.error((error as Error).message || t("crud.queryFailed"));
    } finally {
      if (isMounted.value) {
        loading.value = false;
      }
    }
  };

  const {
    controller: tableViewController,
    tableColumns,
    tableSize,
  } = useTableView<TList>({
    tableKey,
    columns,
    pagination,
    onRefresh: fetchData,
    api: tableViewApi,
    translate: t,
  });

  const onTableChange = (pager: TablePaginationConfig) => {
    pagination.current = pager.current;
    pagination.pageSize = pager.pageSize;
    void fetchData();
  };

  const handleSearch = () => {
    pagination.current = 1;
    void fetchData();
  };

  const resetFilters = () => {
    keyword.value = "";
    handleSearch();
  };

  const resetForm = () => {
    const defaults = defaultFormModel();
    Object.assign(formModel as object, defaults as object);
  };

  const openCreate = () => {
    formMode.value = "create";
    selectedId.value = null;
    resetForm();
    formVisible.value = true;
  };

  const openEdit = async (recordOrId: TList | string) => {
    formMode.value = "edit";
    resetForm();

    if (typeof recordOrId === "string") {
      if (!api.detail) {
        message.error(t("crud.detailByIdNotSupported"));
        return;
      }

      try {
        const detail = await api.detail(recordOrId);
        if (!isMounted.value) return;
        selectedId.value = recordOrId;
        if (mapDetailToForm) {
          mapDetailToForm(detail, formModel);
        }
        formVisible.value = true;
      } catch (error) {
        if (!isMounted.value) return;
        message.error(
          (error as Error).message || t("crud.loadDetailFailed")
        );
      }

      return;
    }

    const record = recordOrId as TList & { id?: string };
    selectedId.value = record.id ?? null;
    if (mapRecordToForm) {
      mapRecordToForm(recordOrId, formModel);
    } else if (mapDetailToForm && api.detail && record.id) {
      try {
        const detail = await api.detail(record.id);
        if (!isMounted.value) return;
        mapDetailToForm(detail, formModel);
      } catch (error) {
        if (!isMounted.value) return;
        message.error(
          (error as Error).message || t("crud.loadDetailFailed")
        );
        return;
      }
    }
    formVisible.value = true;
  };

  const closeForm = () => {
    formVisible.value = false;
  };

  const submitForm = async () => {
    if (submitting.value) {
      return;
    }

    const valid = await formRef.value?.validate().catch(() => false);
    if (!valid || !isMounted.value) {
      return;
    }

    submitting.value = true;
    try {
      if (formMode.value === "create") {
        const payload = buildCreatePayload
          ? buildCreatePayload(formModel)
          : (formModel as unknown as TCreate);
        await api.create(payload);
        if (!isMounted.value) return;
        message.success(t("crud.createSuccess"));
      } else if (selectedId.value) {
        const payload = buildUpdatePayload
          ? buildUpdatePayload(formModel)
          : (formModel as unknown as TUpdate);
        await api.update(selectedId.value, payload);
        if (!isMounted.value) return;
        message.success(t("crud.updateSuccess"));
      }

      formVisible.value = false;
      await fetchData();
      if (!isMounted.value) return;
      if (onAfterSubmit) {
        await onAfterSubmit();
      }
    } catch (error) {
      if (!isMounted.value) return;
      message.error((error as Error).message || t("crud.submitFailed"));
    } finally {
      if (isMounted.value) {
        submitting.value = false;
      }
    }
  };

  const handleDelete = async (id: string) => {
    if (!api.delete) {
      return;
    }

    try {
      await api.delete(id);
      if (!isMounted.value) return;
      message.success(t("crud.deleteSuccess"));
      await fetchData();
      if (!isMounted.value) return;
      if (onAfterDelete) {
        await onAfterDelete();
      }
    } catch (error) {
      if (!isMounted.value) return;
      message.error((error as Error).message || t("crud.deleteFailed"));
    }
  };

  const handleProjectChanged = () => {
    if (!autoFetch) {
      return;
    }
    pagination.current = 1;
    void fetchData();
  };

  onMounted(() => {
    isMounted.value = true;
    if (typeof window !== "undefined") {
      window.addEventListener("project-changed", handleProjectChanged);
    }

    if (autoFetch) {
      void fetchData();
    }
  });

  onUnmounted(() => {
    isMounted.value = false;
    if (typeof window !== "undefined") {
      window.removeEventListener("project-changed", handleProjectChanged);
    }
  });

  return {
    dataSource,
    loading,
    keyword,
    pagination,
    advancedQueryConfig,
    formVisible,
    formMode,
    formRef,
    formModel,
    formRules,
    selectedId,
    submitting,
    tableViewController,
    tableColumns,
    tableSize,
    canCreate,
    canUpdate,
    canDelete,
    hasPermissionFor,
    profile,
    fetchData,
    onTableChange,
    handleSearch,
    resetFilters,
    openCreate,
    openEdit,
    closeForm,
    submitForm,
    handleDelete,
    resetForm,
  };
}

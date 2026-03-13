import { onMounted, reactive, ref } from "vue";
import type { TablePaginationConfig, FormInstance } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import { useTableView } from "@/composables/useTableView";
import type { TableViewColumn } from "@/composables/useTableView";
import type { PagedRequest, PagedResult } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";
import type { FormMode } from "@/utils/common";

export interface CrudApi<TList, TDetail, TCreate extends object, TUpdate extends object> {
  list: (params: any) => Promise<PagedResult<TList>>;
  detail?: (id: string) => Promise<TDetail>;
  create: (data: TCreate) => Promise<any>;
  update: (id: string, data: TUpdate) => Promise<any>;
  delete?: (id: string) => Promise<any>;
}

export interface CrudPermissions {
  create?: string;
  update?: string;
  delete?: string;
  [key: string]: string | undefined;
}

export interface UseCrudPageOptions<TList, TDetail, TCreate extends object, TUpdate extends object> {
  tableKey: string;
  columns: TableViewColumn<TList>[];
  permissions: CrudPermissions;
  api: CrudApi<TList, TDetail, TCreate, TUpdate>;
  defaultFormModel: () => TCreate & TUpdate;
  formRules: Record<string, Rule[]>;
  formRef?: ReturnType<typeof ref<FormInstance>>;
  buildListParams?: (base: PagedRequest) => any;
  buildCreatePayload?: (model: TCreate & TUpdate) => TCreate;
  buildUpdatePayload?: (model: TCreate & TUpdate) => TUpdate;
  mapDetailToForm?: (detail: TDetail, model: TCreate & TUpdate) => void;
  mapRecordToForm?: (record: TList, model: TCreate & TUpdate) => void;
  onAfterSubmit?: () => void | Promise<void>;
  onAfterDelete?: () => void | Promise<void>;
  autoFetch?: boolean;
}

export function useCrudPage<TList, TDetail, TCreate extends object, TUpdate extends object>(
  options: UseCrudPageOptions<TList, TDetail, TCreate, TUpdate>
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
    autoFetch = true
  } = options;

  // State
  const dataSource = ref<TList[]>([]) as { value: TList[] };
  const loading = ref(false);
  const keyword = ref("");
  const pagination = reactive<TablePaginationConfig>({
    current: 1,
    pageSize: 20,
    total: 0,
    showTotal: (total: number) => `共 ${total} 条`
  });

  const formVisible = ref(false);
  const formMode = ref<FormMode>("create");
  const formRef = options.formRef ?? ref<FormInstance>();
  const formModel = reactive<TCreate & TUpdate>(defaultFormModel()) as TCreate & TUpdate;
  const selectedId = ref<string | null>(null);
  const submitting = ref(false);

  // Permissions
  const profile = getAuthProfile();
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

  // Data fetching
  const fetchData = async () => {
    loading.value = true;
    try {
      const baseParams: PagedRequest = {
        pageIndex: pagination.current ?? 1,
        pageSize: pagination.pageSize ?? 20,
        keyword: keyword.value || undefined
      };
      const params = buildListParams ? buildListParams(baseParams) : baseParams;
      const result = await api.list(params);
      dataSource.value = result.items;
      pagination.total = result.total;
    } catch (error) {
      message.error((error as Error).message || "查询失败");
    } finally {
      loading.value = false;
    }
  };

  // Table view
  const { controller: tableViewController, tableColumns, tableSize } = useTableView<TList>({
    tableKey,
    columns,
    pagination,
    onRefresh: fetchData
  });

  // Table change handler
  const onTableChange = (pager: TablePaginationConfig) => {
    pagination.current = pager.current;
    pagination.pageSize = pager.pageSize;
    fetchData();
  };

  // Search
  const handleSearch = () => {
    pagination.current = 1;
    fetchData();
  };

  const resetFilters = () => {
    keyword.value = "";
    handleSearch();
  };

  // Form helpers
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
      // Load by ID using detail API
      if (!api.detail) {
        message.error("不支持按ID加载详情");
        return;
      }
      try {
        const detail = await api.detail(recordOrId);
        selectedId.value = recordOrId;
        if (mapDetailToForm) {
          mapDetailToForm(detail, formModel);
        }
        formVisible.value = true;
      } catch (error) {
        message.error((error as Error).message || "加载详情失败");
      }
    } else {
      // Use record directly
      const record = recordOrId as TList & { id?: string };
      selectedId.value = record.id ?? null;
      if (mapRecordToForm) {
        mapRecordToForm(recordOrId, formModel);
      } else if (mapDetailToForm && api.detail && record.id) {
        try {
          const detail = await api.detail(record.id);
          if (mapDetailToForm) {
            mapDetailToForm(detail, formModel);
          }
        } catch (error) {
          message.error((error as Error).message || "加载详情失败");
          return;
        }
      }
      formVisible.value = true;
    }
  };

  const closeForm = () => {
    formVisible.value = false;
  };

  const submitForm = async () => {
    if (submitting.value) return;

    const valid = await formRef.value?.validate().catch(() => false);
    if (!valid) return;

    submitting.value = true;
    try {
      if (formMode.value === "create") {
        const payload = buildCreatePayload ? buildCreatePayload(formModel) : (formModel as unknown as TCreate);
        await api.create(payload);
        message.success("创建成功");
      } else if (selectedId.value) {
        const payload = buildUpdatePayload ? buildUpdatePayload(formModel) : (formModel as unknown as TUpdate);
        await api.update(selectedId.value, payload);
        message.success("更新成功");
      }
      formVisible.value = false;
      await fetchData();
      if (onAfterSubmit) {
        await onAfterSubmit();
      }
    } catch (error) {
      message.error((error as Error).message || "提交失败");
    } finally {
      submitting.value = false;
    }
  };

  const handleDelete = async (id: string) => {
    if (!api.delete) return;
    try {
      await api.delete(id);
      message.success("删除成功");
      await fetchData();
      if (onAfterDelete) {
        await onAfterDelete();
      }
    } catch (error) {
      message.error((error as Error).message || "删除失败");
    }
  };

  // Lifecycle
  if (autoFetch) {
    onMounted(() => {
      fetchData();
    });
  }

  return {
    // State
    dataSource,
    loading,
    keyword,
    pagination,
    formVisible,
    formMode,
    formRef,
    formModel,
    formRules,
    selectedId,
    submitting,

    // Table view
    tableViewController,
    tableColumns,
    tableSize,

    // Permissions
    canCreate,
    canUpdate,
    canDelete,
    hasPermissionFor,
    profile,

    // Actions
    fetchData,
    onTableChange,
    handleSearch,
    resetFilters,
    openCreate,
    openEdit,
    closeForm,
    submitForm,
    handleDelete,
    resetForm
  };
}

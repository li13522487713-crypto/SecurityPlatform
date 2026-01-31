import { computed, onMounted, reactive, ref, watch } from "vue";
import type { TablePaginationConfig } from "ant-design-vue";
import type { ColumnType } from "ant-design-vue/es/table";
import { message } from "ant-design-vue";
import type {
  TableViewColumnConfig,
  TableViewConfig,
  TableViewCreateRequest,
  TableViewDetail,
  TableViewListItem
} from "@/types/api";
import {
  createTableView,
  getDefaultTableView,
  getDefaultTableViewConfig,
  getTableViewDetail,
  getTableViewsPaged,
  setDefaultTableView,
  updateTableView,
  updateTableViewConfig
} from "@/services/api";

export type TableViewDensity = "compact" | "default" | "comfortable";

export interface TableViewColumnViewMeta {
  canHide?: boolean;
  defaultVisible?: boolean;
}

export type TableViewColumn<TRecord> = ColumnType<TRecord> & {
  view?: TableViewColumnViewMeta;
};

export interface ColumnSettingItem {
  key: string;
  title: string;
  visible: boolean;
  canHide: boolean;
  order: number;
}

export interface TableViewState {
  views: TableViewListItem[];
  loading: boolean;
  currentViewId: string | null;
  currentViewName: string;
  isDefault: boolean;
  density: TableViewDensity;
}

type PinnedType = "left" | "right";
type AlignType = "left" | "center" | "right";

interface NormalizedColumn<TRecord> {
  key: string;
  visible: boolean;
  order: number;
  width?: number;
  pinned?: PinnedType;
  align?: AlignType;
  ellipsis?: boolean;
  canHide: boolean;
  base: TableViewColumn<TRecord>;
}

export interface TableViewController {
  state: TableViewState;
  columnSettings: ColumnSettingItem[];
  searchViews: (keyword?: string) => Promise<void>;
  selectView: (id: string | null) => Promise<void>;
  saveView: () => Promise<void>;
  saveAs: (name: string) => Promise<void>;
  setDefault: () => Promise<void>;
  resetToDefault: () => Promise<void>;
  resetCurrent: () => Promise<void>;
  setDensity: (density: TableViewDensity) => void;
  toggleColumn: (key: string, visible: boolean) => void;
  moveColumn: (key: string, direction: "up" | "down") => void;
}

interface UseTableViewOptions<TRecord> {
  tableKey: string;
  columns: TableViewColumn<TRecord>[];
  pagination: TablePaginationConfig;
  onRefresh?: () => void;
}

const densityToTableSize = (density: TableViewDensity) => {
  if (density === "compact") return "small";
  if (density === "comfortable") return "default";
  return "middle";
};

const resolveColumnKey = <TRecord>(column: TableViewColumn<TRecord>) => {
  if (typeof column.key === "string" && column.key.trim()) return column.key;
  if (typeof column.key === "number") return column.key.toString();
  if (typeof column.dataIndex === "string" && column.dataIndex.trim()) return column.dataIndex;
  if (Array.isArray(column.dataIndex) && column.dataIndex.length > 0) {
    return column.dataIndex.join(".");
  }
  return "";
};

const resolveColumnTitle = <TRecord>(column: TableViewColumn<TRecord>) => {
  if (typeof column.title === "string") return column.title;
  const key = resolveColumnKey(column);
  return key || "未命名列";
};

const resolvePinned = (fixed: ColumnType<unknown>["fixed"]) => {
  return fixed === "left" || fixed === "right" ? fixed : undefined;
};

const resolveAlign = (align: ColumnType<unknown>["align"]) => {
  return align === "left" || align === "center" || align === "right" ? align : undefined;
};

const buildDefaultConfig = <TRecord>(
  columns: TableViewColumn<TRecord>[],
  pageSize: number
): TableViewConfig => {
  const configColumns: TableViewColumnConfig[] = columns.map((column, index) => {
    const key = resolveColumnKey(column);
    const defaultVisible = column.view?.defaultVisible ?? true;
    return {
      key,
      visible: defaultVisible,
      order: index,
      width: typeof column.width === "number" ? column.width : undefined,
      pinned: resolvePinned(column.fixed),
      align: resolveAlign(column.align),
      ellipsis: typeof column.ellipsis === "boolean" ? column.ellipsis : undefined
    };
  });

  return {
    columns: configColumns,
    density: "default",
    pagination: { pageSize }
  };
};

const normalizeColumns = <TRecord>(
  columns: TableViewColumn<TRecord>[],
  configColumns: TableViewColumnConfig[]
): NormalizedColumn<TRecord>[] => {
  const configMap = new Map(configColumns.map((item) => [item.key, item]));
  const normalized = columns.map((column, index) => {
    const key = resolveColumnKey(column);
    const config = configMap.get(key);
    const canHide = column.view?.canHide !== false;
    const visible = canHide ? (config?.visible ?? true) : true;
    return {
      key,
      visible,
      order: typeof config?.order === "number" ? config.order : index,
      width: config?.width,
      pinned: config?.pinned,
      align: config?.align,
      ellipsis: config?.ellipsis,
      canHide,
      base: column
    };
  });

  normalized.sort((a, b) => a.order - b.order);
  normalized.forEach((item, index) => {
    item.order = index;
  });
  return normalized;
};

const toColumnConfig = <TRecord>(items: NormalizedColumn<TRecord>[]): TableViewColumnConfig[] =>
  items.map((item) => ({
    key: item.key,
    visible: item.visible,
    order: item.order,
    width: item.width,
    pinned: item.pinned,
    align: item.align,
    ellipsis: item.ellipsis
  }));

export function useTableView<TRecord>(options: UseTableViewOptions<TRecord>) {
  const { tableKey, columns, pagination, onRefresh } = options;
  const defaultConfig = buildDefaultConfig(columns, pagination.pageSize ?? 10);
  const config = ref<TableViewConfig>({ ...defaultConfig });
  const state = reactive<TableViewState>({
    views: [],
    loading: false,
    currentViewId: null,
    currentViewName: "未保存",
    isDefault: false,
    density: (defaultConfig.density ?? "default") as TableViewDensity
  });

  const tableColumns = computed(() => {
    const normalized = normalizeColumns(columns, config.value.columns ?? []);
    return normalized
      .filter((item) => item.visible || !item.canHide)
      .map((item) => ({
        ...item.base,
        key: item.key,
        width: item.width ?? item.base.width,
        fixed: item.pinned ?? item.base.fixed,
        align: item.align ?? item.base.align,
        ellipsis: item.ellipsis ?? item.base.ellipsis
      }));
  });

  const tableSize = computed(() => densityToTableSize(state.density));

  const columnSettings = computed<ColumnSettingItem[]>(() => {
    const normalized = normalizeColumns(columns, config.value.columns ?? []);
    return normalized.map((item) => ({
      key: item.key,
      title: resolveColumnTitle(item.base),
      visible: item.visible,
      canHide: item.canHide,
      order: item.order
    }));
  });

  let suppressAutoSave = false;
  let isApplyingConfig = false;
  let saveTimer: number | undefined;

  const scheduleAutoSave = () => {
    if (!state.currentViewId || suppressAutoSave) return;
    if (saveTimer) {
      window.clearTimeout(saveTimer);
    }
    saveTimer = window.setTimeout(() => {
      void persistConfig();
    }, 400);
  };

  const applyConfig = (detail: TableViewDetail | null, refresh = true) => {
    suppressAutoSave = true;
    isApplyingConfig = true;
    if (detail) {
      config.value = {
        ...detail.config,
        columns: normalizeColumns(columns, detail.config.columns ?? []).map((item) => ({
          key: item.key,
          visible: item.visible,
          order: item.order,
          width: item.width,
          pinned: item.pinned,
          align: item.align,
          ellipsis: item.ellipsis
        }))
      };
      state.currentViewId = detail.id;
      state.currentViewName = detail.name;
      state.isDefault = detail.isDefault;
      state.density = (detail.config.density ?? "default") as TableViewDensity;
      const pageSize = detail.config.pagination?.pageSize;
      if (pageSize && pagination.pageSize !== pageSize) {
        pagination.pageSize = pageSize;
        pagination.current = 1;
      }
    } else {
      config.value = { ...defaultConfig };
      state.currentViewId = null;
      state.currentViewName = "未保存";
      state.isDefault = false;
      state.density = (defaultConfig.density ?? "default") as TableViewDensity;
    }
    suppressAutoSave = false;
    queueMicrotask(() => {
      isApplyingConfig = false;
    });
    if (refresh) {
      onRefresh?.();
    }
  };

  const loadViews = async (keyword?: string) => {
    state.loading = true;
    try {
      const result = await getTableViewsPaged(tableKey, {
        pageIndex: 1,
        pageSize: 20,
        keyword: keyword?.trim() || undefined
      });
      state.views = result.items;
    } catch (error) {
      message.error((error as Error).message || "加载视图失败");
    } finally {
      state.loading = false;
    }
  };

  const loadDefault = async () => {
    try {
      const detail = await getDefaultTableView(tableKey);
      applyConfig(detail, false);
    } catch (error) {
      applyConfig(null, false);
    }
  };

  const selectView = async (id: string | null) => {
    if (!id) {
      applyConfig(null);
      return;
    }
    try {
      const detail = await getTableViewDetail(id);
      applyConfig(detail);
    } catch (error) {
      message.error((error as Error).message || "加载视图失败");
    }
  };

  const persistConfig = async () => {
    if (!state.currentViewId) return;
    const payload = {
      config: {
        ...config.value,
        columns: toColumnConfig(normalizeColumns(columns, config.value.columns ?? []))
      }
    };
    try {
      await updateTableViewConfig(state.currentViewId, payload);
    } catch (error) {
      message.error((error as Error).message || "保存视图失败");
    }
  };

  const saveView = async () => {
    if (!state.currentViewId) return;
    const payload = {
      name: state.currentViewName,
      config: {
        ...config.value,
        columns: toColumnConfig(normalizeColumns(columns, config.value.columns ?? []))
      }
    };
    try {
      await updateTableView(state.currentViewId, payload);
      await loadViews();
      message.success("视图已保存");
    } catch (error) {
      message.error((error as Error).message || "保存视图失败");
    }
  };

  const saveAs = async (name: string) => {
    const payload: TableViewCreateRequest = {
      tableKey,
      name,
      config: {
        ...config.value,
        columns: toColumnConfig(normalizeColumns(columns, config.value.columns ?? []))
      }
    };
    try {
      const result = await createTableView(payload);
      await loadViews();
      state.currentViewId = result.id;
      state.currentViewName = name;
      state.isDefault = false;
      message.success("视图已保存");
    } catch (error) {
      message.error((error as Error).message || "保存视图失败");
    }
  };

  const setDefault = async () => {
    if (!state.currentViewId) return;
    try {
      await setDefaultTableView(state.currentViewId);
      state.isDefault = true;
      await loadViews();
      message.success("已设为默认视图");
    } catch (error) {
      message.error((error as Error).message || "设置默认视图失败");
    }
  };

  const resetToDefault = async () => {
    const currentId = state.currentViewId;
    const currentName = state.currentViewName;
    const currentDefault = state.isDefault;
    let targetConfig = { ...defaultConfig };
    try {
      const serverConfig = await getDefaultTableViewConfig(tableKey);
      if (serverConfig) {
        targetConfig = {
          ...defaultConfig,
          ...serverConfig,
          columns:
            serverConfig.columns && serverConfig.columns.length > 0
              ? serverConfig.columns
              : defaultConfig.columns
        };
      }
    } catch {
      targetConfig = { ...defaultConfig };
    }
    suppressAutoSave = true;
    isApplyingConfig = true;
    config.value = {
      ...targetConfig,
      columns: normalizeColumns(columns, targetConfig.columns ?? []).map((item) => ({
        key: item.key,
        visible: item.visible,
        order: item.order,
        width: item.width,
        pinned: item.pinned,
        align: item.align,
        ellipsis: item.ellipsis
      }))
    };
    state.density = (targetConfig.density ?? "default") as TableViewDensity;
    if (targetConfig.pagination?.pageSize && pagination.pageSize !== targetConfig.pagination.pageSize) {
      pagination.pageSize = targetConfig.pagination.pageSize;
      pagination.current = 1;
    }
    if (currentId) {
      state.currentViewId = currentId;
      state.currentViewName = currentName;
      state.isDefault = currentDefault;
    } else {
      state.currentViewId = null;
      state.currentViewName = "未保存";
      state.isDefault = false;
    }
    suppressAutoSave = false;
    queueMicrotask(() => {
      isApplyingConfig = false;
    });
    onRefresh?.();
    if (currentId) {
      await persistConfig();
    }
  };

  const resetCurrent = async () => {
    if (!state.currentViewId) {
      applyConfig(null);
      return;
    }
    await selectView(state.currentViewId);
  };

  const setDensity = (density: TableViewDensity) => {
    config.value = {
      ...config.value,
      density
    };
    state.density = density;
    scheduleAutoSave();
  };

  const updateConfigColumns = (updater: (items: NormalizedColumn<TRecord>[]) => void) => {
    const normalized = normalizeColumns(columns, config.value.columns ?? []);
    updater(normalized);
    config.value = {
      ...config.value,
      columns: toColumnConfig(normalized)
    };
    scheduleAutoSave();
  };

  const toggleColumn = (key: string, visible: boolean) => {
    updateConfigColumns((items) => {
      const target = items.find((item) => item.key === key);
      if (target && target.canHide) {
        target.visible = visible;
      }
    });
  };

  const moveColumn = (key: string, direction: "up" | "down") => {
    updateConfigColumns((items) => {
      const index = items.findIndex((item) => item.key === key);
      if (index < 0) return;
      const targetIndex = direction === "up" ? index - 1 : index + 1;
      if (targetIndex < 0 || targetIndex >= items.length) return;
      const [moved] = items.splice(index, 1);
      items.splice(targetIndex, 0, moved);
      items.forEach((item, idx) => {
        item.order = idx;
      });
    });
  };

  watch(
    () => pagination.pageSize,
    (value, oldValue) => {
      if (suppressAutoSave || isApplyingConfig) return;
      if (!value || value === oldValue) return;
      config.value = {
        ...config.value,
        pagination: { pageSize: value }
      };
      scheduleAutoSave();
    }
  );

  const searchViews = async (keyword?: string) => {
    await loadViews(keyword);
  };

  onMounted(() => {
    void loadViews();
    void loadDefault();
  });

  const controller = reactive<TableViewController>({
    state,
    columnSettings: columnSettings.value,
    searchViews,
    selectView,
    saveView,
    saveAs,
    setDefault,
    resetToDefault,
    resetCurrent,
    setDensity,
    toggleColumn,
    moveColumn
  });

  watch(columnSettings, (settings) => {
    controller.columnSettings = settings;
  });

  return {
    controller,
    tableColumns,
    tableSize
  };
}

const commonTermMap: Record<string, { zh: string; en: string }> = {
  ai: { zh: "AI", en: "AI" },
  admin: { zh: "管理员", en: "Admin" },
  approval: { zh: "审批", en: "Approval" },
  app: { zh: "应用", en: "App" },
  archived: { zh: "已归档", en: "Archived" },
  actions: { zh: "操作", en: "Actions" },
  all: { zh: "全部", en: "All" },
  basic: { zh: "基础", en: "Basic" },
  cancel: { zh: "取消", en: "Cancel" },
  clear: { zh: "清空", en: "Clear" },
  columns: { zh: "列", en: "Columns" },
  compact: { zh: "紧凑", en: "Compact" },
  comfortable: { zh: "舒适", en: "Comfortable" },
  confirm: { zh: "确认", en: "Confirm" },
  created: { zh: "创建", en: "Created" },
  createdat: { zh: "创建时间", en: "Created At" },
  crud: { zh: "增删改查", en: "CRUD" },
  data: { zh: "数据", en: "Data" },
  default: { zh: "默认", en: "Default" },
  delete: { zh: "删除", en: "Delete" },
  detail: { zh: "详情", en: "Detail" },
  density: { zh: "密度", en: "Density" },
  disabled: { zh: "停用", en: "Disabled" },
  edit: { zh: "编辑", en: "Edit" },
  enter: { zh: "输入", en: "Enter" },
  failed: { zh: "失败", en: "Failed" },
  filter: { zh: "筛选", en: "Filter" },
  form: { zh: "表单", en: "Form" },
  key: { zh: "键", en: "Key" },
  left: { zh: "左侧", en: "Left" },
  load: { zh: "加载", en: "Load" },
  name: { zh: "名称", en: "Name" },
  none: { zh: "无", en: "None" },
  operation: { zh: "操作", en: "Operation" },
  pin: { zh: "固定", en: "Pin" },
  placeholder: { zh: "占位", en: "Placeholder" },
  query: { zh: "查询", en: "Query" },
  reset: { zh: "重置", en: "Reset" },
  right: { zh: "右侧", en: "Right" },
  save: { zh: "保存", en: "Save" },
  search: { zh: "搜索", en: "Search" },
  set: { zh: "设置", en: "Set" },
  status: { zh: "状态", en: "Status" },
  submit: { zh: "提交", en: "Submit" },
  table: { zh: "表格", en: "Table" },
  tableview: { zh: "表格视图", en: "Table View" },
  title: { zh: "标题", en: "Title" },
  total: { zh: "总数", en: "Total" },
  update: { zh: "更新", en: "Update" },
  view: { zh: "视图", en: "View" }
};

function splitKeyToTokens(key: string): string[] {
  const normalized = key
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/[_-]/g, " ")
    .replace(/\./g, " ");
  return normalized
    .split(/\s+/)
    .map((token) => token.trim())
    .filter((token) => token.length > 0);
}

function toTitleCase(input: string): string {
  if (!input) return input;
  return input.charAt(0).toUpperCase() + input.slice(1).toLowerCase();
}

function buildEnglishFallback(key: string): string {
  const tokens = splitKeyToTokens(key);
  if (tokens.length === 0) return key;
  return tokens
    .map((token) => {
      const mapped = commonTermMap[token.toLowerCase()];
      if (mapped?.en) return mapped.en;
      return toTitleCase(token);
    })
    .join(" ");
}

function buildChineseFallback(key: string): string {
  const tokens = splitKeyToTokens(key);
  if (tokens.length === 0) return key;
  return tokens
    .map((token) => {
      const mapped = commonTermMap[token.toLowerCase()];
      if (mapped?.zh) return mapped.zh;
      return token;
    })
    .join(" ");
}

export function formatMissingI18nKey(locale: string, key: string): string {
  if (!key || !key.includes(".")) return key;
  if (locale.toLowerCase().startsWith("zh")) {
    return buildChineseFallback(key);
  }
  return buildEnglishFallback(key);
}

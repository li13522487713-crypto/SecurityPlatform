/**
 * 【表单 II-2.2 选择控件】
 * 8 类选择控件 Schema 工厂函数
 * select / multi-select / checkboxes / radios / list-select / button-group-select / transfer / input-tree
 */
import type { AmisSchema } from "@/types/amis";

/** 选项类型 */
export interface OptionItem {
  label: string;
  value: string | number;
  children?: OptionItem[];
  disabled?: boolean;
}

/** 选择控件通用选项 */
export interface SelectBaseOptions {
  name: string;
  label?: string;
  value?: unknown;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  description?: string;
  options?: OptionItem[];
  source?: string;
  searchable?: boolean;
  clearable?: boolean;
  multiple?: boolean;
  joinValues?: boolean;
  extractValue?: boolean;
  delimiter?: string;
  labelField?: string;
  valueField?: string;
}

function selectBase(type: string, opts: SelectBaseOptions, extra: Record<string, unknown> = {}): AmisSchema {
  return {
    type,
    name: opts.name,
    label: opts.label ?? opts.name,
    ...(opts.value !== undefined ? { value: opts.value } : {}),
    ...(opts.placeholder ? { placeholder: opts.placeholder } : {}),
    ...(opts.required ? { required: true } : {}),
    ...(opts.disabled ? { disabled: true } : {}),
    ...(opts.description ? { description: opts.description } : {}),
    ...(opts.options ? { options: opts.options } : {}),
    ...(opts.source ? { source: opts.source } : {}),
    ...(opts.searchable ? { searchable: true } : {}),
    ...(opts.clearable ? { clearable: true } : {}),
    ...(opts.multiple ? { multiple: true } : {}),
    ...(opts.joinValues !== undefined ? { joinValues: opts.joinValues } : {}),
    ...(opts.extractValue ? { extractValue: true } : {}),
    ...(opts.delimiter ? { delimiter: opts.delimiter } : {}),
    ...(opts.labelField ? { labelField: opts.labelField } : {}),
    ...(opts.valueField ? { valueField: opts.valueField } : {}),
    ...extra,
  };
}

/** 下拉选择 */
export function select(opts: SelectBaseOptions & {
  autoComplete?: string;
  creatable?: boolean;
  checkAll?: boolean;
  checkAllLabel?: string;
  menuTpl?: string;
  valuesNoWrap?: boolean;
} = { name: "select" }): AmisSchema {
  return selectBase("select", opts, {
    ...(opts.autoComplete ? { autoComplete: opts.autoComplete } : {}),
    ...(opts.creatable ? { creatable: true } : {}),
    ...(opts.checkAll ? { checkAll: true } : {}),
    ...(opts.checkAllLabel ? { checkAllLabel: opts.checkAllLabel } : {}),
    ...(opts.menuTpl ? { menuTpl: opts.menuTpl } : {}),
    ...(opts.valuesNoWrap ? { valuesNoWrap: true } : {}),
  });
}

/** 多选（select 的 multiple 封装） */
export function multiSelect(opts: Omit<SelectBaseOptions, "multiple"> & {
  checkAll?: boolean;
  checkAllLabel?: string;
} = { name: "multiSelect" }): AmisSchema {
  return selectBase("select", { ...opts, multiple: true }, {
    ...(opts.checkAll ? { checkAll: true } : {}),
    ...(opts.checkAllLabel ? { checkAllLabel: opts.checkAllLabel } : {}),
  });
}

/** 复选框组 */
export function checkboxes(opts: SelectBaseOptions & {
  columnsCount?: number;
  checkAll?: boolean;
  inline?: boolean;
} = { name: "checkboxes" }): AmisSchema {
  return selectBase("checkboxes", opts, {
    ...(opts.columnsCount ? { columnsCount: opts.columnsCount } : {}),
    ...(opts.checkAll ? { checkAll: true } : {}),
    ...(opts.inline !== false ? { inline: opts.inline ?? true } : {}),
  });
}

/** 单选框组 */
export function radios(opts: SelectBaseOptions & {
  columnsCount?: number;
  inline?: boolean;
} = { name: "radios" }): AmisSchema {
  return selectBase("radios", opts, {
    ...(opts.columnsCount ? { columnsCount: opts.columnsCount } : {}),
    ...(opts.inline !== false ? { inline: opts.inline ?? true } : {}),
  });
}

/** 列表选择 */
export function listSelect(opts: SelectBaseOptions & {
  imageClassName?: string;
  listClassName?: string;
} = { name: "listSelect" }): AmisSchema {
  return selectBase("list-select", opts, {
    ...(opts.imageClassName ? { imageClassName: opts.imageClassName } : {}),
    ...(opts.listClassName ? { listClassName: opts.listClassName } : {}),
  });
}

/** 按钮组选择 */
export function buttonGroupSelect(opts: SelectBaseOptions & {
  btnLevel?: string;
  btnActiveLevel?: string;
  vertical?: boolean;
  tiled?: boolean;
} = { name: "buttonGroup" }): AmisSchema {
  return selectBase("button-group-select", opts, {
    ...(opts.btnLevel ? { btnLevel: opts.btnLevel } : {}),
    ...(opts.btnActiveLevel ? { btnActiveLevel: opts.btnActiveLevel } : {}),
    ...(opts.vertical ? { vertical: true } : {}),
    ...(opts.tiled ? { tiled: true } : {}),
  });
}

/** 穿梭器 */
export function transfer(opts: SelectBaseOptions & {
  selectMode?: "list" | "table" | "tree" | "chained" | "associated";
  columns?: AmisSchema[];
  resultListModeFollowSelect?: boolean;
  showArrow?: boolean;
  sortable?: boolean;
  statisticsTextFormatter?: string;
} = { name: "transfer" }): AmisSchema {
  return selectBase("transfer", opts, {
    ...(opts.selectMode ? { selectMode: opts.selectMode } : {}),
    ...(opts.columns ? { columns: opts.columns } : {}),
    ...(opts.resultListModeFollowSelect ? { resultListModeFollowSelect: true } : {}),
    ...(opts.showArrow !== false ? { showArrow: opts.showArrow ?? true } : {}),
    ...(opts.sortable ? { sortable: true } : {}),
    ...(opts.statisticsTextFormatter ? { statisticsTextFormatter: opts.statisticsTextFormatter } : {}),
  });
}

/** 树形选择 */
export function inputTree(opts: SelectBaseOptions & {
  showIcon?: boolean;
  showOutline?: boolean;
  cascade?: boolean;
  withChildren?: boolean;
  initiallyOpen?: boolean;
  unfoldedLevel?: number;
  autoCheckChildren?: boolean;
  heightAuto?: boolean;
} = { name: "tree" }): AmisSchema {
  return selectBase("input-tree", opts, {
    ...(opts.showIcon ? { showIcon: true } : {}),
    ...(opts.showOutline ? { showOutline: true } : {}),
    ...(opts.cascade ? { cascade: true } : {}),
    ...(opts.withChildren ? { withChildren: true } : {}),
    ...(opts.initiallyOpen ? { initiallyOpen: true } : {}),
    ...(opts.unfoldedLevel !== undefined ? { unfoldedLevel: opts.unfoldedLevel } : {}),
    ...(opts.autoCheckChildren !== false ? { autoCheckChildren: opts.autoCheckChildren ?? true } : {}),
    ...(opts.heightAuto ? { heightAuto: true } : {}),
  });
}

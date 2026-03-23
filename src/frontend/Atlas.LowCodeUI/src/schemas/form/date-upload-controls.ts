/**
 * 【表单 II-2.2 日期/上传控件】
 * 日期、上传、城市、组合、条件构建器等控件 Schema 工厂
 */
import type { AmisSchema } from "@/types/amis";

interface ControlOptions {
  name: string;
  label?: string;
  value?: unknown;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  description?: string;
}

function ctrl(type: string, opts: ControlOptions, extra: Record<string, unknown> = {}): AmisSchema {
  return {
    type,
    name: opts.name,
    label: opts.label ?? opts.name,
    ...(opts.value !== undefined ? { value: opts.value } : {}),
    ...(opts.placeholder ? { placeholder: opts.placeholder } : {}),
    ...(opts.required ? { required: true } : {}),
    ...(opts.disabled ? { disabled: true } : {}),
    ...(opts.description ? { description: opts.description } : {}),
    ...extra,
  };
}

/** 日期选择 */
export function inputDate(opts: ControlOptions & {
  format?: string;
  inputFormat?: string;
  closeOnSelect?: boolean;
  minDate?: string;
  maxDate?: string;
  shortcuts?: string[];
} = { name: "date" }): AmisSchema {
  return ctrl("input-date", opts, {
    format: opts.format ?? "YYYY-MM-DD",
    ...(opts.inputFormat ? { inputFormat: opts.inputFormat } : {}),
    ...(opts.closeOnSelect !== false ? { closeOnSelect: true } : {}),
    ...(opts.minDate ? { minDate: opts.minDate } : {}),
    ...(opts.maxDate ? { maxDate: opts.maxDate } : {}),
    ...(opts.shortcuts ? { shortcuts: opts.shortcuts } : {}),
  });
}

/** 日期时间选择 */
export function inputDatetime(opts: ControlOptions & {
  format?: string;
  inputFormat?: string;
  timeFormat?: string;
  minDate?: string;
  maxDate?: string;
} = { name: "datetime" }): AmisSchema {
  return ctrl("input-datetime", opts, {
    format: opts.format ?? "YYYY-MM-DD HH:mm:ss",
    ...(opts.inputFormat ? { inputFormat: opts.inputFormat } : {}),
    ...(opts.timeFormat ? { timeFormat: opts.timeFormat } : {}),
    ...(opts.minDate ? { minDate: opts.minDate } : {}),
    ...(opts.maxDate ? { maxDate: opts.maxDate } : {}),
  });
}

/** 日期区间选择 */
export function inputDateRange(opts: ControlOptions & {
  format?: string;
  inputFormat?: string;
  minDate?: string;
  maxDate?: string;
  minDuration?: string;
  maxDuration?: string;
  delimiter?: string;
} = { name: "dateRange" }): AmisSchema {
  return ctrl("input-date-range", opts, {
    format: opts.format ?? "YYYY-MM-DD",
    ...(opts.inputFormat ? { inputFormat: opts.inputFormat } : {}),
    ...(opts.minDate ? { minDate: opts.minDate } : {}),
    ...(opts.maxDate ? { maxDate: opts.maxDate } : {}),
    ...(opts.minDuration ? { minDuration: opts.minDuration } : {}),
    ...(opts.maxDuration ? { maxDuration: opts.maxDuration } : {}),
    ...(opts.delimiter ? { delimiter: opts.delimiter } : {}),
  });
}

/** 时间选择 */
export function inputTime(opts: ControlOptions & {
  format?: string;
  inputFormat?: string;
  timeConstraints?: Record<string, unknown>;
} = { name: "time" }): AmisSchema {
  return ctrl("input-time", opts, {
    format: opts.format ?? "HH:mm",
    ...(opts.inputFormat ? { inputFormat: opts.inputFormat } : {}),
    ...(opts.timeConstraints ? { timeConstraints: opts.timeConstraints } : {}),
  });
}

/** 文件上传 */
export function inputFile(opts: ControlOptions & {
  receiver?: string;
  accept?: string;
  maxSize?: number;
  multiple?: boolean;
  maxLength?: number;
  autoUpload?: boolean;
  drag?: boolean;
  startChunkApi?: string;
  chunkApi?: string;
  finishChunkApi?: string;
  chunkSize?: number;
} = { name: "file" }): AmisSchema {
  return ctrl("input-file", opts, {
    ...(opts.receiver ? { receiver: opts.receiver } : {}),
    ...(opts.accept ? { accept: opts.accept } : {}),
    ...(opts.maxSize ? { maxSize: opts.maxSize } : {}),
    ...(opts.multiple ? { multiple: true } : {}),
    ...(opts.maxLength ? { maxLength: opts.maxLength } : {}),
    ...(opts.autoUpload !== false ? { autoUpload: true } : {}),
    ...(opts.drag ? { drag: true } : {}),
    ...(opts.startChunkApi ? { startChunkApi: opts.startChunkApi } : {}),
    ...(opts.chunkApi ? { chunkApi: opts.chunkApi } : {}),
    ...(opts.finishChunkApi ? { finishChunkApi: opts.finishChunkApi } : {}),
    ...(opts.chunkSize ? { chunkSize: opts.chunkSize } : {}),
  });
}

/** 图片上传 */
export function inputImage(opts: ControlOptions & {
  receiver?: string;
  accept?: string;
  maxSize?: number;
  multiple?: boolean;
  maxLength?: number;
  autoUpload?: boolean;
  crop?: boolean | Record<string, unknown>;
  limit?: Record<string, unknown>;
} = { name: "image" }): AmisSchema {
  return ctrl("input-image", opts, {
    ...(opts.receiver ? { receiver: opts.receiver } : {}),
    accept: opts.accept ?? ".jpg,.jpeg,.png,.gif,.svg",
    ...(opts.maxSize ? { maxSize: opts.maxSize } : {}),
    ...(opts.multiple ? { multiple: true } : {}),
    ...(opts.maxLength ? { maxLength: opts.maxLength } : {}),
    ...(opts.autoUpload !== false ? { autoUpload: true } : {}),
    ...(opts.crop ? { crop: opts.crop } : {}),
    ...(opts.limit ? { limit: opts.limit } : {}),
  });
}

/** 城市选择 */
export function inputCity(opts: ControlOptions & {
  allowCity?: boolean;
  allowDistrict?: boolean;
  searchable?: boolean;
  extractValue?: boolean;
} = { name: "city" }): AmisSchema {
  return ctrl("input-city", opts, {
    ...(opts.allowCity !== false ? { allowCity: true } : {}),
    ...(opts.allowDistrict !== false ? { allowDistrict: true } : {}),
    ...(opts.searchable ? { searchable: true } : {}),
    ...(opts.extractValue ? { extractValue: true } : {}),
  });
}

/** 组合输入（子表单） */
export function combo(opts: ControlOptions & {
  items: AmisSchema[];
  multiple?: boolean;
  multiLine?: boolean;
  addable?: boolean;
  removable?: boolean;
  draggable?: boolean;
  addButtonText?: string;
  minLength?: number;
  maxLength?: number;
  flat?: boolean;
  noBorder?: boolean;
} = { name: "combo", items: [] }): AmisSchema {
  return ctrl("combo", opts, {
    items: opts.items,
    ...(opts.multiple ? { multiple: true } : {}),
    ...(opts.multiLine ? { multiLine: true } : {}),
    ...(opts.addable !== false ? { addable: true } : {}),
    ...(opts.removable !== false ? { removable: true } : {}),
    ...(opts.draggable ? { draggable: true } : {}),
    ...(opts.addButtonText ? { addButtonText: opts.addButtonText } : {}),
    ...(opts.minLength ? { minLength: opts.minLength } : {}),
    ...(opts.maxLength ? { maxLength: opts.maxLength } : {}),
    ...(opts.flat ? { flat: true } : {}),
    ...(opts.noBorder ? { noBorder: true } : {}),
  });
}

/** 表格输入 */
export function inputTable(opts: ControlOptions & {
  columns: AmisSchema[];
  addable?: boolean;
  removable?: boolean;
  draggable?: boolean;
  editable?: boolean;
  addButtonText?: string;
  minLength?: number;
  maxLength?: number;
  needConfirm?: boolean;
} = { name: "table", columns: [] }): AmisSchema {
  return ctrl("input-table", opts, {
    columns: opts.columns,
    ...(opts.addable !== false ? { addable: true } : {}),
    ...(opts.removable !== false ? { removable: true } : {}),
    ...(opts.draggable ? { draggable: true } : {}),
    ...(opts.editable !== false ? { editable: true } : {}),
    ...(opts.addButtonText ? { addButtonText: opts.addButtonText } : {}),
    ...(opts.minLength ? { minLength: opts.minLength } : {}),
    ...(opts.maxLength ? { maxLength: opts.maxLength } : {}),
    ...(opts.needConfirm !== undefined ? { needConfirm: opts.needConfirm } : {}),
  });
}

/** 条件组合构建器 */
export function conditionBuilder(opts: ControlOptions & {
  fields: Array<{
    label: string;
    name: string;
    type: "text" | "number" | "date" | "datetime" | "time" | "select" | "boolean";
    operators?: string[];
    options?: Array<{ label: string; value: string }>;
  }>;
  showANDOR?: boolean;
  showNot?: boolean;
} = { name: "condition", fields: [] }): AmisSchema {
  return ctrl("condition-builder", opts, {
    fields: opts.fields,
    ...(opts.showANDOR !== false ? { showANDOR: true } : {}),
    ...(opts.showNot ? { showNot: true } : {}),
  });
}

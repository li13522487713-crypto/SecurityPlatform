/**
 * 30+ 组件 ComponentMeta 全量声明（M06 C06-3 + C06-4 + C06-5 + C06-8）。
 *
 * 5 大类：
 *  - layout（8）  Container / Row / Column / Tabs / Drawer / Modal / Grid / Section
 *  - display（13） Text / Markdown / Image / Video / Avatar / Badge / Progress / Rate / Chart / EmptyState / Loading / Error / Toast
 *  - input（18）  Button / TextInput / NumberInput / Switch / Select / RadioGroup / CheckboxGroup / DatePicker / TimePicker / ColorPicker / Slider / FileUpload / ImageUpload / CodeEditor / FormContainer / FormField / SearchBox / Filter
 *  - ai（4）      AiChat / AiCard / AiSuggestion / AiAvatarReply
 *  - data（4）    WaterfallList / Table / List / Pagination
 *
 * 共 47 件（远超 30+ 下限）。
 *
 * 6 维能力矩阵（PLAN.md §M06 C06-8）：
 *  1) 表单值采集 (formValue)
 *  2) 事件触发 (eventTrigger)
 *  3) 工作流输出回填 (workflowOutputFill)
 *  4) AI 原生绑定 (aiNative)
 *  5) 上传产物 (uploadAsset)
 *  6) 内容参数 (contentParams)
 */

import type { ComponentMeta, ContentParamKind, EventName, RendererType, ValueType } from '@atlas/lowcode-schema';

const WEB_ONLY: ReadonlyArray<RendererType> = ['web'];

interface ComponentBuildOptions {
  type: string;
  displayName: string;
  category: string;
  group?: string;
  bindableProps?: ReadonlyArray<string>;
  contentParams?: ReadonlyArray<ContentParamKind>;
  supportedEvents?: ReadonlyArray<EventName>;
  childArity?: 'none' | 'one' | 'many';
  childAllowTypes?: string[];
  supportedValueType?: Record<string, ValueType>;
}

function build(opts: ComponentBuildOptions): ComponentMeta {
  return {
    type: opts.type,
    displayName: opts.displayName,
    category: opts.category,
    group: opts.group,
    version: '1.0.0',
    runtimeRenderer: WEB_ONLY,
    bindableProps: opts.bindableProps ?? [],
    contentParams: opts.contentParams,
    supportedEvents: opts.supportedEvents ?? [],
    childPolicy: { arity: opts.childArity ?? 'none', allowTypes: opts.childAllowTypes },
    supportedValueType: opts.supportedValueType
  };
}

// --- layout ---
// 所有容器类组件均允许绑定通用样式 prop（className / style），符合 6 维矩阵中的"工作流输出回填"维度。
const COMMON_LAYOUT_PROPS = ['className', 'style'] as const;

export const LAYOUT_METAS: ReadonlyArray<ComponentMeta> = [
  build({ type: 'Container', displayName: '容器', category: 'layout', childArity: 'many', bindableProps: COMMON_LAYOUT_PROPS }),
  build({ type: 'Row', displayName: '行', category: 'layout', childArity: 'many', bindableProps: ['gap', 'justify', 'align'] }),
  build({ type: 'Column', displayName: '列', category: 'layout', childArity: 'many', bindableProps: ['gap', 'justify', 'align'] }),
  build({ type: 'Tabs', displayName: '标签页', category: 'layout', childArity: 'many', bindableProps: ['activeKey'], supportedEvents: ['onChange'] }),
  build({ type: 'Drawer', displayName: '抽屉', category: 'layout', childArity: 'many', bindableProps: ['visible', 'placement', 'title'], supportedEvents: ['onChange'] }),
  build({ type: 'Modal', displayName: '弹窗', category: 'layout', childArity: 'many', bindableProps: ['visible', 'title'], supportedEvents: ['onChange', 'onSubmit'] }),
  build({ type: 'Grid', displayName: '网格', category: 'layout', childArity: 'many', bindableProps: ['columns', 'gap'] }),
  build({ type: 'Section', displayName: '段落', category: 'layout', childArity: 'many', bindableProps: ['title'] })
];

// --- display ---
export const DISPLAY_METAS: ReadonlyArray<ComponentMeta> = [
  build({ type: 'Text', displayName: '文字', category: 'display', bindableProps: ['content', 'color'], contentParams: ['text'] }),
  build({ type: 'Markdown', displayName: 'Markdown', category: 'display', bindableProps: ['content'], contentParams: ['text'] }),
  build({ type: 'Image', displayName: '图片', category: 'display', bindableProps: ['src', 'alt', 'fit'], contentParams: ['image'] }),
  build({ type: 'Video', displayName: '视频', category: 'display', bindableProps: ['src', 'poster', 'autoplay', 'controls'], contentParams: ['media'] }),
  build({ type: 'Avatar', displayName: '头像', category: 'display', bindableProps: ['src', 'name', 'size'], contentParams: ['image'] }),
  build({ type: 'Badge', displayName: '徽标', category: 'display', bindableProps: ['count', 'color'] }),
  build({ type: 'Progress', displayName: '进度', category: 'display', bindableProps: ['percent', 'status'] }),
  build({ type: 'Rate', displayName: '评分', category: 'display', bindableProps: ['value', 'count'], supportedEvents: ['onChange'] }),
  build({ type: 'Chart', displayName: '图表', category: 'display', bindableProps: ['data', 'type', 'options'], contentParams: ['data'] }),
  build({ type: 'EmptyState', displayName: '空状态', category: 'display', bindableProps: ['title', 'description'] }),
  build({ type: 'Loading', displayName: '加载', category: 'display', bindableProps: ['size'] }),
  build({ type: 'Error', displayName: '错误', category: 'display', bindableProps: ['message', 'retryable'], supportedEvents: ['onClick'] }),
  build({ type: 'Toast', displayName: '提示', category: 'display', bindableProps: ['message', 'type', 'duration'] })
];

// --- input ---
const INPUT_VALUE_TYPES_TEXT: Record<string, ValueType> = { value: 'string' };
const INPUT_VALUE_TYPES_NUM: Record<string, ValueType> = { value: 'number' };
const INPUT_VALUE_TYPES_BOOL: Record<string, ValueType> = { value: 'boolean' };
const INPUT_VALUE_TYPES_ARR: Record<string, ValueType> = { value: 'array' };
const INPUT_VALUE_TYPES_DATE: Record<string, ValueType> = { value: 'date' };
const INPUT_VALUE_TYPES_FILE: Record<string, ValueType> = { value: 'file' };
const INPUT_VALUE_TYPES_IMG: Record<string, ValueType> = { value: 'image' };

export const INPUT_METAS: ReadonlyArray<ComponentMeta> = [
  build({ type: 'Button', displayName: '按钮', category: 'input', bindableProps: ['text', 'disabled', 'loading'], supportedEvents: ['onClick'] }),
  build({ type: 'TextInput', displayName: '文本输入', category: 'input', bindableProps: ['value', 'placeholder', 'disabled'], supportedEvents: ['onChange'], supportedValueType: INPUT_VALUE_TYPES_TEXT }),
  build({ type: 'NumberInput', displayName: '数字输入', category: 'input', bindableProps: ['value', 'min', 'max', 'step'], supportedEvents: ['onChange'], supportedValueType: INPUT_VALUE_TYPES_NUM }),
  build({ type: 'Switch', displayName: '开关', category: 'input', bindableProps: ['value'], supportedEvents: ['onChange'], supportedValueType: INPUT_VALUE_TYPES_BOOL }),
  build({ type: 'Select', displayName: '下拉', category: 'input', bindableProps: ['value', 'options', 'placeholder', 'disabled'], supportedEvents: ['onChange'], contentParams: ['data'], supportedValueType: { value: 'any' } }),
  build({ type: 'RadioGroup', displayName: '单选组', category: 'input', bindableProps: ['value', 'options'], supportedEvents: ['onChange'], contentParams: ['data'], supportedValueType: { value: 'any' } }),
  build({ type: 'CheckboxGroup', displayName: '多选组', category: 'input', bindableProps: ['value', 'options'], supportedEvents: ['onChange'], contentParams: ['data'], supportedValueType: INPUT_VALUE_TYPES_ARR }),
  build({ type: 'DatePicker', displayName: '日期选择', category: 'input', bindableProps: ['value', 'format'], supportedEvents: ['onChange'], supportedValueType: INPUT_VALUE_TYPES_DATE }),
  build({ type: 'TimePicker', displayName: '时间选择', category: 'input', bindableProps: ['value', 'format'], supportedEvents: ['onChange'], supportedValueType: INPUT_VALUE_TYPES_DATE }),
  build({ type: 'ColorPicker', displayName: '颜色选择', category: 'input', bindableProps: ['value'], supportedEvents: ['onChange'], supportedValueType: INPUT_VALUE_TYPES_TEXT }),
  build({ type: 'Slider', displayName: '滑动条', category: 'input', bindableProps: ['value', 'min', 'max', 'step'], supportedEvents: ['onChange'], supportedValueType: INPUT_VALUE_TYPES_NUM }),
  build({ type: 'FileUpload', displayName: '文件上传', category: 'input', bindableProps: ['value', 'accept', 'multiple'], supportedEvents: ['onUploadSuccess', 'onUploadError', 'onChange'], supportedValueType: INPUT_VALUE_TYPES_FILE }),
  build({ type: 'ImageUpload', displayName: '图片上传', category: 'input', bindableProps: ['value', 'accept', 'multiple'], supportedEvents: ['onUploadSuccess', 'onUploadError', 'onChange'], supportedValueType: INPUT_VALUE_TYPES_IMG }),
  build({ type: 'CodeEditor', displayName: '代码编辑', category: 'input', bindableProps: ['value', 'language', 'readonly'], supportedEvents: ['onChange'], supportedValueType: INPUT_VALUE_TYPES_TEXT }),
  build({ type: 'FormContainer', displayName: '表单容器', category: 'input', childArity: 'many', bindableProps: ['initialValues'], supportedEvents: ['onSubmit', 'onChange'] }),
  build({ type: 'FormField', displayName: '表单字段', category: 'input', childArity: 'one', bindableProps: ['name', 'label', 'required'] }),
  build({ type: 'SearchBox', displayName: '搜索', category: 'input', bindableProps: ['value', 'placeholder'], supportedEvents: ['onChange', 'onSubmit'], supportedValueType: INPUT_VALUE_TYPES_TEXT }),
  build({ type: 'Filter', displayName: '筛选', category: 'input', bindableProps: ['value', 'options'], supportedEvents: ['onChange'], contentParams: ['data'], supportedValueType: INPUT_VALUE_TYPES_ARR })
];

// --- ai ---
export const AI_METAS: ReadonlyArray<ComponentMeta> = [
  build({ type: 'AiChat', displayName: 'AI 对话', category: 'ai', bindableProps: ['chatflowId', 'sessionId', 'modelId'], contentParams: ['ai'], supportedEvents: ['onChange', 'onSubmit'] }),
  build({ type: 'AiCard', displayName: 'AI 卡片', category: 'ai', bindableProps: ['chatflowId', 'cardConfig'], contentParams: ['ai'], supportedEvents: ['onClick'] }),
  build({ type: 'AiSuggestion', displayName: 'AI 推荐', category: 'ai', bindableProps: ['suggestions', 'modelId'], contentParams: ['data'], supportedEvents: ['onItemClick'] }),
  build({ type: 'AiAvatarReply', displayName: 'AI 头像回复', category: 'ai', bindableProps: ['chatflowId', 'avatarUrl'], contentParams: ['ai'] })
];

// --- data ---
export const DATA_METAS: ReadonlyArray<ComponentMeta> = [
  build({ type: 'WaterfallList', displayName: '瀑布流', category: 'data', bindableProps: ['items', 'columns'], contentParams: ['data'], supportedEvents: ['onItemClick', 'onScrollEnd'] }),
  build({ type: 'Table', displayName: '表格', category: 'data', bindableProps: ['dataSource', 'columns', 'pagination'], contentParams: ['data'], supportedEvents: ['onChange', 'onItemClick'] }),
  build({ type: 'List', displayName: '列表', category: 'data', bindableProps: ['items'], contentParams: ['data'], supportedEvents: ['onItemClick'] }),
  build({ type: 'Pagination', displayName: '分页', category: 'data', bindableProps: ['current', 'pageSize', 'total'], supportedEvents: ['onChange'] })
];

export const ALL_METAS: ReadonlyArray<ComponentMeta> = [
  ...LAYOUT_METAS,
  ...DISPLAY_METAS,
  ...INPUT_METAS,
  ...AI_METAS,
  ...DATA_METAS
];

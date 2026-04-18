/**
 * 47 组件实现 type 清单（M06 P1-1）。
 *
 * 单独的 .ts 文件（无 React/Semi 依赖），便于：
 *  1) 单测在 node 环境快速校验"meta type 与实现 type 完全一致"，无需 import 组件 .tsx；
 *  2) lowcode-runtime-web / lowcode-debug-client 静态守门：判断某 type 是否有实现。
 */

export const LAYOUT_IMPLEMENTATION_TYPES = ['Container', 'Row', 'Column', 'Tabs', 'Drawer', 'Modal', 'Grid', 'Section'] as const;
export const DISPLAY_IMPLEMENTATION_TYPES = [
  'Text',
  'Markdown',
  'Image',
  'Video',
  'Avatar',
  'Badge',
  'Progress',
  'Rate',
  'Chart',
  'EmptyState',
  'Loading',
  'Error',
  'Toast'
] as const;
export const INPUT_IMPLEMENTATION_TYPES = [
  'Button',
  'TextInput',
  'NumberInput',
  'Switch',
  'Select',
  'RadioGroup',
  'CheckboxGroup',
  'DatePicker',
  'TimePicker',
  'ColorPicker',
  'Slider',
  'FileUpload',
  'ImageUpload',
  'CodeEditor',
  'FormContainer',
  'FormField',
  'SearchBox',
  'Filter'
] as const;
export const AI_IMPLEMENTATION_TYPES = ['AiChat', 'AiCard', 'AiSuggestion', 'AiAvatarReply'] as const;
export const DATA_IMPLEMENTATION_TYPES = ['WaterfallList', 'Table', 'List', 'Pagination'] as const;

export const ALL_IMPLEMENTATION_TYPES = [
  ...LAYOUT_IMPLEMENTATION_TYPES,
  ...DISPLAY_IMPLEMENTATION_TYPES,
  ...INPUT_IMPLEMENTATION_TYPES,
  ...AI_IMPLEMENTATION_TYPES,
  ...DATA_IMPLEMENTATION_TYPES
] as const;

export const IMPLEMENTATION_TYPE_SET: ReadonlySet<string> = new Set(ALL_IMPLEMENTATION_TYPES);

export function hasImplementation(type: string): boolean {
  return IMPLEMENTATION_TYPE_SET.has(type);
}

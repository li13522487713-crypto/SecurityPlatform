// src/pages/lowcode/custom-designer/core/types.ts

export interface ComponentSchema {
  id: string;
  type: string;
  name: string;
  props: Record<string, any>;
  styles: Record<string, any>;
  children?: ComponentSchema[];
}

export type PropEditorType = 'string' | 'number' | 'boolean' | 'select' | 'color' | 'event' | 'json';

export interface PropConfig {
  name: string;
  label: string;
  type: PropEditorType;
  options?: { label: string; value: string | number }[];
  defaultValue?: any;
}

export interface ComponentMeta {
  type: string;
  name: string;
  icon: string;
  category: 'basic' | 'container' | 'data' | 'feedback';
  defaultSchema: Omit<ComponentSchema, 'id'>;
  propsGroup: {
    basic?: PropConfig[];
    style?: PropConfig[];
    data?: PropConfig[];
    event?: PropConfig[];
    permission?: PropConfig[];
  };
}

import type { ComponentMeta } from './types';

export const COMPONENT_REGISTRY: Record<string, ComponentMeta> = {
  container: {
    type: 'container',
    name: '普通容器',
    icon: 'LayoutOutlined',
    category: 'container',
    defaultSchema: {
      type: 'container',
      name: '容器',
      props: {},
      styles: { minHeight: '50px', padding: '16px', backgroundColor: '#ffffff', border: '1px dashed #d9d9d9', borderRadius: '4px', marginBottom: '16px' },
      children: []
    },
    propsGroup: {
      style: [
        { name: 'padding', label: '内边距', type: 'string', defaultValue: '16px' },
        { name: 'margin', label: '外边距', type: 'string', defaultValue: '0' },
        { name: 'backgroundColor', label: '背景色', type: 'color', defaultValue: '#ffffff' },
        { name: 'borderRadius', label: '圆角', type: 'string', defaultValue: '4px' }
      ]
    }
  },
  card: {
    type: 'card',
    name: '卡片',
    icon: 'IdcardOutlined',
    category: 'container',
    defaultSchema: {
      type: 'card',
      name: '卡片',
      props: { title: '默认卡片', bordered: true },
      styles: { marginBottom: '16px' },
      children: []
    },
    propsGroup: {
      basic: [
        { name: 'title', label: '卡片标题', type: 'string', defaultValue: '默认卡片' },
        { name: 'bordered', label: '是否有边框', type: 'boolean', defaultValue: true }
      ]
    }
  },
  text: {
    type: 'text',
    name: '文本',
    icon: 'FontSizeOutlined',
    category: 'basic',
    defaultSchema: {
      type: 'text',
      name: '文本',
      props: { text: '示例文本' },
      styles: { fontSize: '14px', color: '#333333', marginBottom: '8px', display: 'block' }
    },
    propsGroup: {
      basic: [
        { name: 'text', label: '文本内容', type: 'string', defaultValue: '示例文本' }
      ],
      style: [
        { name: 'fontSize', label: '字号', type: 'string', defaultValue: '14px' },
        { name: 'color', label: '颜色', type: 'color', defaultValue: '#333333' },
        { name: 'fontWeight', label: '字重', type: 'select', options: [{label:'正常', value:'normal'}, {label:'加粗', value:'bold'}], defaultValue: 'normal' },
        { name: 'textAlign', label: '对齐方式', type: 'select', options: [{label:'左侧', value:'left'}, {label:'居中', value:'center'}, {label:'右侧', value:'right'}], defaultValue: 'left' }
      ]
    }
  },
  button: {
    type: 'button',
    name: '按钮',
    icon: 'PlayCircleOutlined',
    category: 'basic',
    defaultSchema: {
      type: 'button',
      name: '按钮',
      props: { text: '按钮', type: 'primary', disabled: false },
      styles: { margin: '0 8px 8px 0' }
    },
    propsGroup: {
      basic: [
        { name: 'text', label: '按钮文本', type: 'string', defaultValue: '按钮' },
        { name: 'type', label: '按钮类型', type: 'select', 
          options: [
            {label: '主按钮(Primary)', value: 'primary'},
            {label: '次按钮(Default)', value: 'default'},
            {label: '虚线(Dashed)', value: 'dashed'},
            {label: '文本(Text)', value: 'text'},
            {label: '链接(Link)', value: 'link'}
          ], defaultValue: 'primary' 
        },
        { name: 'danger', label: '危险状态', type: 'boolean', defaultValue: false },
        { name: 'disabled', label: '是否禁用', type: 'boolean', defaultValue: false }
      ],
      event: [
        { name: 'onClick', label: '点击事件', type: 'event' }
      ]
    }
  },
  input: {
    type: 'input',
    name: '输入框',
    icon: 'EditOutlined',
    category: 'basic',
    defaultSchema: {
      type: 'input',
      name: '输入框',
      props: { placeholder: '请输入内容', disabled: false, allowClear: true },
      styles: { width: '200px', margin: '0 8px 8px 0' }
    },
    propsGroup: {
      basic: [
        { name: 'placeholder', label: '占位提示', type: 'string', defaultValue: '请输入内容' },
        { name: 'allowClear', label: '允许清除', type: 'boolean', defaultValue: true },
        { name: 'disabled', label: '是否禁用', type: 'boolean', defaultValue: false }
      ],
      style: [
        { name: 'width', label: '宽度', type: 'string', defaultValue: '200px' }
      ]
    }
  },
  select: {
    type: 'select',
    name: '下拉选择',
    icon: 'DownSquareOutlined',
    category: 'basic',
    defaultSchema: {
      type: 'select',
      name: '下拉选择',
      props: { placeholder: '请选择', options: [{label: '选项一', value: '1'}, {label: '选项二', value: '2'}], disabled: false },
      styles: { width: '200px', margin: '0 8px 8px 0' }
    },
    propsGroup: {
      basic: [
        { name: 'placeholder', label: '占位提示', type: 'string', defaultValue: '请选择' },
        { name: 'disabled', label: '是否禁用', type: 'boolean', defaultValue: false }
      ],
      data: [
        { name: 'options', label: '选项数据', type: 'json' }
      ],
      style: [
        { name: 'width', label: '宽度', type: 'string', defaultValue: '200px' }
      ]
    }
  },
  table: {
    type: 'table',
    name: '表格',
    icon: 'TableOutlined',
    category: 'data',
    defaultSchema: {
      type: 'table',
      name: '数据表格',
      props: {
        columns: [
          { title: '客户名称', dataIndex: 'name' },
          { title: '行业', dataIndex: 'industry' },
          { title: '负责人', dataIndex: 'owner' },
          { title: '状态', dataIndex: 'status' },
          { title: '更新时间', dataIndex: 'updatedAt' }
        ],
        dataSource: [
          { id: 1, name: '示例数据', industry: '示例数据', owner: '示例数据', status: '示例数据', updatedAt: '示例数据' },
          { id: 2, name: '示例数据', industry: '示例数据', owner: '示例数据', status: '示例数据', updatedAt: '示例数据' }
        ],
        pagination: false,
        size: 'middle'
      },
      styles: { marginTop: '16px', background: '#fff' }
    },
    propsGroup: {
      basic: [
        { name: 'size', label: '尺寸', type: 'select', options: [{label: '默认', value:'default'}, {label:'中等', value:'middle'}, {label:'紧凑', value:'small'}], defaultValue: 'middle' },
        { name: 'pagination', label: '显示分页', type: 'boolean', defaultValue: false }
      ],
      data: [
        { name: 'dataSource', label: '静态数据(Mock)', type: 'json' },
        { name: 'columns', label: '列配置', type: 'json' }
      ]
    }
  }
};

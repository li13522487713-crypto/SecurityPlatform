/**
 * 输入类组件实现（M06 P1-1，18 件）：
 * Button / TextInput / NumberInput / Switch / Select / RadioGroup / CheckboxGroup
 * DatePicker / TimePicker / ColorPicker / Slider / FileUpload / ImageUpload / CodeEditor
 * FormContainer / FormField / SearchBox / Filter
 */
import * as React from 'react';
import {
  Button as SemiButton,
  CheckboxGroup as SemiCheckboxGroup,
  Checkbox,
  DatePicker,
  Form,
  Input as SemiInput,
  InputNumber,
  RadioGroup as SemiRadioGroup,
  Radio,
  Select as SemiSelect,
  Slider as SemiSlider,
  Switch as SemiSwitch,
  TimePicker as SemiTimePicker,
  Upload
} from '@douyinfe/semi-ui';
import { IconSearch, IconFilter } from '@douyinfe/semi-icons';
import type { ComponentRenderer } from './runtime-types';

interface OptionItem { label: string; value: string | number }

const Button: ComponentRenderer = ({ props, fireEvent }) => (
  <SemiButton
    type="primary"
    disabled={Boolean(props.disabled)}
    loading={Boolean(props.loading)}
    onClick={() => fireEvent('onClick', null)}
  >
    {typeof props.text === 'string' ? props.text : '按钮'}
  </SemiButton>
);

const TextInput: ComponentRenderer = ({ props, fireEvent }) => (
  <SemiInput
    value={typeof props.value === 'string' ? props.value : ''}
    placeholder={typeof props.placeholder === 'string' ? props.placeholder : ''}
    disabled={Boolean(props.disabled)}
    onChange={(v) => fireEvent('onChange', { value: v })}
  />
);

const NumberInput: ComponentRenderer = ({ props, fireEvent }) => (
  <InputNumber
    value={typeof props.value === 'number' ? props.value : undefined}
    min={typeof props.min === 'number' ? props.min : undefined}
    max={typeof props.max === 'number' ? props.max : undefined}
    step={typeof props.step === 'number' ? props.step : 1}
    onChange={(v) => fireEvent('onChange', { value: v })}
  />
);

const Switch: ComponentRenderer = ({ props, fireEvent }) => (
  <SemiSwitch checked={Boolean(props.value)} onChange={(checked) => fireEvent('onChange', { value: checked })} />
);

const Select: ComponentRenderer = ({ props, fireEvent, getContentParam }) => {
  const opts = resolveOptions(props.options ?? getContentParam?.('data'));
  return (
    <SemiSelect
      value={props.value as string | number | undefined}
      placeholder={typeof props.placeholder === 'string' ? props.placeholder : ''}
      disabled={Boolean(props.disabled)}
      style={{ minWidth: 160 }}
      onChange={(v) => fireEvent('onChange', { value: v })}
    >
      {opts.map((o) => (
        <SemiSelect.Option key={String(o.value)} value={o.value}>
          {o.label}
        </SemiSelect.Option>
      ))}
    </SemiSelect>
  );
};

const RadioGroup: ComponentRenderer = ({ props, fireEvent, getContentParam }) => {
  const opts = resolveOptions(props.options ?? getContentParam?.('data'));
  return (
    <SemiRadioGroup value={props.value as string | number | undefined} onChange={(e) => fireEvent('onChange', { value: e.target.value })}>
      {opts.map((o) => (
        <Radio key={String(o.value)} value={o.value}>
          {o.label}
        </Radio>
      ))}
    </SemiRadioGroup>
  );
};

const CheckboxGroup: ComponentRenderer = ({ props, fireEvent, getContentParam }) => {
  const opts = resolveOptions(props.options ?? getContentParam?.('data'));
  return (
    <SemiCheckboxGroup
      value={(Array.isArray(props.value) ? props.value : []) as Array<string | number>}
      onChange={(v) => fireEvent('onChange', { value: v })}
    >
      {opts.map((o) => (
        <Checkbox key={String(o.value)} value={o.value}>
          {o.label}
        </Checkbox>
      ))}
    </SemiCheckboxGroup>
  );
};

const DatePickerImpl: ComponentRenderer = ({ props, fireEvent }) => (
  <DatePicker
    value={typeof props.value === 'string' || typeof props.value === 'number' ? new Date(props.value as string | number) : undefined}
    format={typeof props.format === 'string' ? props.format : 'yyyy-MM-dd'}
    onChange={(v) => fireEvent('onChange', { value: v })}
  />
);

const TimePicker: ComponentRenderer = ({ props, fireEvent }) => (
  <SemiTimePicker
    value={typeof props.value === 'string' ? props.value : undefined}
    format={typeof props.format === 'string' ? props.format : 'HH:mm:ss'}
    onChange={(v) => fireEvent('onChange', { value: v })}
  />
);

const ColorPicker: ComponentRenderer = ({ props, fireEvent }) => (
  // Semi 暂无独立 ColorPicker，使用 input[type=color] 提供基础能力（生产可由调用方替换为更丰富实现）
  <input
    type="color"
    value={typeof props.value === 'string' ? props.value : '#000000'}
    onChange={(e) => fireEvent('onChange', { value: e.target.value })}
  />
);

const Slider: ComponentRenderer = ({ props, fireEvent }) => (
  <SemiSlider
    value={typeof props.value === 'number' ? props.value : undefined}
    min={typeof props.min === 'number' ? props.min : 0}
    max={typeof props.max === 'number' ? props.max : 100}
    step={typeof props.step === 'number' ? props.step : 1}
    onChange={(v) => fireEvent('onChange', { value: v })}
  />
);

const FileUpload: ComponentRenderer = ({ props, fireEvent }) => (
  <Upload
    accept={typeof props.accept === 'string' ? props.accept : undefined}
    multiple={Boolean(props.multiple)}
    customRequest={({ onSuccess, fileInstance }) => {
      // 真实上传由 lowcode-asset-adapter 注入的 RuntimeContext 处理；本组件不直调任何 /api/runtime/files
      // 这里 onSuccess 提交"指令型"事件，让 dispatch 走两阶段 prepare/complete 协议
      fireEvent('onUploadSuccess', { name: fileInstance.name, size: fileInstance.size });
      onSuccess({ status: 200 });
    }}
    onChange={() => fireEvent('onChange', null)}
  >
    选择文件
  </Upload>
);

const ImageUpload: ComponentRenderer = ({ props, fireEvent }) => (
  <Upload
    accept={typeof props.accept === 'string' ? props.accept : 'image/*'}
    multiple={Boolean(props.multiple)}
    listType="picture"
    customRequest={({ onSuccess, fileInstance }) => {
      fireEvent('onUploadSuccess', { name: fileInstance.name, size: fileInstance.size, kind: 'image' });
      onSuccess({ status: 200 });
    }}
    onChange={() => fireEvent('onChange', null)}
  >
    选择图片
  </Upload>
);

const CodeEditor: ComponentRenderer = ({ props, fireEvent }) => (
  // Monaco 由 lowcode-property-forms 在设计器内嵌；运行时使用 textarea 作为安全回退（不引入 monaco 进运行时打包）
  <textarea
    value={typeof props.value === 'string' ? props.value : ''}
    readOnly={Boolean(props.readonly)}
    onChange={(e) => fireEvent('onChange', { value: e.target.value })}
    style={{ fontFamily: 'Menlo, monospace', minHeight: 120, width: '100%' }}
    data-language={typeof props.language === 'string' ? props.language : 'plaintext'}
  />
);

const FormContainer: ComponentRenderer = ({ children, fireEvent }) => (
  <Form onSubmit={(values) => fireEvent('onSubmit', values)} onValueChange={(values) => fireEvent('onChange', values)}>
    {children}
  </Form>
);

const FormField: ComponentRenderer = ({ props, children }) => (
  <Form.Slot label={typeof props.label === 'string' ? props.label : (typeof props.name === 'string' ? props.name : '')}>
    {children}
  </Form.Slot>
);

const SearchBox: ComponentRenderer = ({ props, fireEvent }) => (
  <SemiInput
    value={typeof props.value === 'string' ? props.value : ''}
    placeholder={typeof props.placeholder === 'string' ? props.placeholder : '搜索...'}
    prefix={<IconSearch />}
    onChange={(v) => fireEvent('onChange', { value: v })}
    onEnterPress={(e) => fireEvent('onSubmit', { value: (e.target as HTMLInputElement).value })}
  />
);

const Filter: ComponentRenderer = ({ props, fireEvent, getContentParam }) => {
  const opts = resolveOptions(props.options ?? getContentParam?.('data'));
  return (
    <div style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
      <IconFilter />
      <SemiSelect
        multiple
        value={Array.isArray(props.value) ? (props.value as Array<string | number>) : []}
        onChange={(v) => fireEvent('onChange', { value: v })}
        style={{ minWidth: 200 }}
      >
        {opts.map((o) => (
          <SemiSelect.Option key={String(o.value)} value={o.value}>
            {o.label}
          </SemiSelect.Option>
        ))}
      </SemiSelect>
    </div>
  );
};

export const INPUT_COMPONENTS: Record<string, ComponentRenderer> = {
  Button,
  TextInput,
  NumberInput,
  Switch,
  Select,
  RadioGroup,
  CheckboxGroup,
  DatePicker: DatePickerImpl,
  TimePicker,
  ColorPicker,
  Slider,
  FileUpload,
  ImageUpload,
  CodeEditor,
  FormContainer,
  FormField,
  SearchBox,
  Filter
};

function resolveOptions(raw: unknown): OptionItem[] {
  if (!Array.isArray(raw)) return [];
  return raw
    .map((item): OptionItem | null => {
      if (item == null) return null;
      if (typeof item === 'string' || typeof item === 'number') return { label: String(item), value: item };
      if (typeof item === 'object' && 'value' in (item as Record<string, unknown>)) {
        const v = (item as Record<string, unknown>).value as string | number;
        const l = (item as Record<string, unknown>).label;
        return { label: typeof l === 'string' ? l : String(v), value: v };
      }
      return null;
    })
    .filter((x): x is OptionItem => x !== null);
}

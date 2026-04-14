import { Button, Input, Select, Space, Switch, Typography } from "@douyinfe/semi-ui";
import { IconDelete, IconPlus } from "@douyinfe/semi-icons";
import type { AppInputComponent } from "../types";
import { createEmptyInput } from "./app-builder-helpers";

const INPUT_TYPE_OPTIONS: Array<{ label: string; value: AppInputComponent["type"] }> = [
  { label: "单行文本", value: "text" },
  { label: "多行文本", value: "textarea" },
  { label: "数字", value: "number" },
  { label: "日期", value: "date" },
  { label: "下拉", value: "select" },
  { label: "文件", value: "file" }
];

export interface InputComponentPanelProps {
  value: AppInputComponent[];
  onChange: (next: AppInputComponent[]) => void;
  disabled?: boolean;
}

export function InputComponentPanel({ value, onChange, disabled }: InputComponentPanelProps) {
  function updateAt(index: number, patch: Partial<AppInputComponent>) {
    const next = value.map((row, i) => (i === index ? { ...row, ...patch } : row));
    onChange(next);
  }

  function removeAt(index: number) {
    onChange(value.filter((_, i) => i !== index));
  }

  function addRow() {
    onChange([...value, createEmptyInput()]);
  }

  return (
    <div className="module-studio__coze-inspector-card module-studio__app-builder-panel">
      <div className="module-studio__card-head">
        <span>输入组件</span>
        <Typography.Text type="tertiary" size="small">
          {value.length} 项
        </Typography.Text>
      </div>
      <Typography.Text type="tertiary" size="small" style={{ display: "block", marginTop: 0 }}>
        定义表单字段与变量键，预览与运行将按变量键组装入参。
      </Typography.Text>
      <div className="module-studio__app-builder-array">
        {value.length === 0 ? (
          <Typography.Text type="tertiary">暂无输入项，请点击下方添加。</Typography.Text>
        ) : (
          value.map((row, index) => (
            <div key={row.id} className="module-studio__app-builder-row">
              <div className="module-studio__form-grid">
                <div className="module-studio__field">
                  <span>标签</span>
                  <Input
                    value={row.label}
                    disabled={disabled}
                    placeholder="显示名称"
                    onChange={v => updateAt(index, { label: v })}
                  />
                </div>
                <div className="module-studio__field">
                  <span>变量键</span>
                  <Input
                    value={row.variableKey}
                    disabled={disabled}
                    placeholder="如 userQuery"
                    onChange={v => updateAt(index, { variableKey: v })}
                  />
                </div>
                <div className="module-studio__field">
                  <span>类型</span>
                  <Select
                    value={row.type}
                    disabled={disabled}
                    optionList={INPUT_TYPE_OPTIONS}
                    onChange={v => {
                      const nextType = v as AppInputComponent["type"];
                      updateAt(index, {
                        type: nextType,
                        options: nextType === "select" ? row.options?.length ? row.options : [{ label: "选项 A", value: "a" }] : undefined
                      });
                    }}
                  />
                </div>
                <div className="module-studio__field module-studio__switch-item">
                  <span>必填</span>
                  <Switch
                    checked={row.required}
                    disabled={disabled}
                    onChange={checked => updateAt(index, { required: checked })}
                  />
                </div>
                {row.type === "select" ? (
                  <div className="module-studio__field module-studio__field--full">
                    <span>选项（标签 / 值）</span>
                    <div className="module-studio__stack">
                      {(row.options ?? []).map((opt, optIndex) => (
                        <Space key={`${row.id}-opt-${optIndex}`} wrap>
                          <Input
                            value={opt.label}
                            disabled={disabled}
                            placeholder="标签"
                            onChange={v => {
                              const options = [...(row.options ?? [])];
                              options[optIndex] = { ...options[optIndex], label: v };
                              updateAt(index, { options });
                            }}
                          />
                          <Input
                            value={opt.value}
                            disabled={disabled}
                            placeholder="值"
                            onChange={v => {
                              const options = [...(row.options ?? [])];
                              options[optIndex] = { ...options[optIndex], value: v };
                              updateAt(index, { options });
                            }}
                          />
                          <Button
                            icon={<IconDelete />}
                            type="danger"
                            theme="borderless"
                            disabled={disabled}
                            onClick={() => {
                              const options = (row.options ?? []).filter((_, j) => j !== optIndex);
                              updateAt(index, { options });
                            }}
                          />
                        </Space>
                      ))}
                      <Button
                        icon={<IconPlus />}
                        theme="light"
                        disabled={disabled}
                        onClick={() => {
                          const options = [...(row.options ?? []), { label: "", value: "" }];
                          updateAt(index, { options });
                        }}
                      >
                        添加选项
                      </Button>
                    </div>
                  </div>
                ) : null}
                <div className="module-studio__field module-studio__field--full">
                  <span>默认值（可选）</span>
                  <Input
                    value={row.defaultValue ?? ""}
                    disabled={disabled}
                    placeholder="未填写时的默认值"
                    onChange={v => updateAt(index, { defaultValue: v || undefined })}
                  />
                </div>
              </div>
              <div className="module-studio__app-builder-row-actions">
                <Button icon={<IconDelete />} type="danger" theme="borderless" disabled={disabled} onClick={() => removeAt(index)}>
                  删除
                </Button>
              </div>
            </div>
          ))
        )}
      </div>
      <Button icon={<IconPlus />} theme="light" disabled={disabled} onClick={addRow} block>
        添加输入项
      </Button>
    </div>
  );
}

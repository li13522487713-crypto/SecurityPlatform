import { Button, Input, Select, Typography } from "@douyinfe/semi-ui";
import { IconDelete, IconPlus } from "@douyinfe/semi-icons";
import type { AppOutputComponent } from "../types";
import { createEmptyOutput } from "./app-builder-helpers";

const OUTPUT_TYPE_OPTIONS: Array<{ label: string; value: AppOutputComponent["type"] }> = [
  { label: "纯文本", value: "text" },
  { label: "Markdown", value: "markdown" },
  { label: "JSON", value: "json" },
  { label: "表格", value: "table" },
  { label: "图表", value: "chart" }
];

export interface OutputComponentPanelProps {
  value: AppOutputComponent[];
  onChange: (next: AppOutputComponent[]) => void;
  disabled?: boolean;
}

export function OutputComponentPanel({ value, onChange, disabled }: OutputComponentPanelProps) {
  function updateAt(index: number, patch: Partial<AppOutputComponent>) {
    onChange(value.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  function removeAt(index: number) {
    onChange(value.filter((_, i) => i !== index));
  }

  function addRow() {
    onChange([...value, createEmptyOutput()]);
  }

  return (
    <div className="module-studio__coze-inspector-card module-studio__app-builder-panel">
      <div className="module-studio__card-head">
        <span>输出组件</span>
        <Typography.Text type="tertiary" size="small">
          {value.length} 项
        </Typography.Text>
      </div>
      <Typography.Text type="tertiary" size="small" style={{ display: "block", marginTop: 0 }}>
        使用源表达式从运行结果中取值（支持顶层键或点路径，如 result.items）。
      </Typography.Text>
      <div className="module-studio__app-builder-array">
        {value.length === 0 ? (
          <Typography.Text type="tertiary">暂无输出项。</Typography.Text>
        ) : (
          value.map((row, index) => (
            <div key={row.id} className="module-studio__app-builder-row">
              <div className="module-studio__form-grid">
                <div className="module-studio__field">
                  <span>标签</span>
                  <Input
                    value={row.label}
                    disabled={disabled}
                    placeholder="展示标题"
                    onChange={v => updateAt(index, { label: v })}
                  />
                </div>
                <div className="module-studio__field">
                  <span>展示类型</span>
                  <Select
                    value={row.type}
                    disabled={disabled}
                    optionList={OUTPUT_TYPE_OPTIONS}
                    onChange={v => updateAt(index, { type: v as AppOutputComponent["type"] })}
                  />
                </div>
                <div className="module-studio__field module-studio__field--full">
                  <span>源表达式</span>
                  <Input
                    value={row.sourceExpression}
                    disabled={disabled}
                    placeholder="如 answer 或 data.summary"
                    onChange={v => updateAt(index, { sourceExpression: v })}
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
        添加输出项
      </Button>
    </div>
  );
}

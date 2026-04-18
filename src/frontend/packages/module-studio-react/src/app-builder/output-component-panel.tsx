import { Button, Input, Select, Typography } from "@douyinfe/semi-ui";
import { IconDelete, IconPlus } from "@douyinfe/semi-icons";
import type { AppOutputComponent, StudioLocale } from "../types";
import { createEmptyOutput } from "./app-builder-helpers";
import { getStudioCopy } from "../copy";

export interface OutputComponentPanelProps {
  value: AppOutputComponent[];
  onChange: (next: AppOutputComponent[]) => void;
  disabled?: boolean;
  locale: StudioLocale;
}

export function OutputComponentPanel({ value, onChange, disabled, locale }: OutputComponentPanelProps) {
  const copy = getStudioCopy(locale);

  /* Markdown / JSON 是国际通用术语，沿用原文不再走字典；其余通过 copy.outputComponent 翻译。 */
  const outputTypeOptions: Array<{ label: string; value: AppOutputComponent["type"] }> = [
    { label: copy.outputComponent.typeText, value: "text" },
    { label: "Markdown", value: "markdown" },
    { label: "JSON", value: "json" },
    { label: copy.outputComponent.typeTable, value: "table" },
    { label: copy.outputComponent.typeChart, value: "chart" }
  ];

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
        <span>{copy.outputComponent.cardTitle}</span>
        <Typography.Text type="tertiary" size="small">
          {value.length} {copy.outputComponent.itemSuffix}
        </Typography.Text>
      </div>
      <Typography.Text type="tertiary" size="small" style={{ display: "block", marginTop: 0 }}>
        {copy.outputComponent.bodyHint}
      </Typography.Text>
      <div className="module-studio__app-builder-array">
        {value.length === 0 ? (
          <Typography.Text type="tertiary">{copy.outputComponent.emptyHint}</Typography.Text>
        ) : (
          value.map((row, index) => (
            <div key={row.id} className="module-studio__app-builder-row">
              <div className="module-studio__form-grid">
                <div className="module-studio__field">
                  <span>{copy.outputComponent.fieldLabel}</span>
                  <Input
                    value={row.label}
                    disabled={disabled}
                    placeholder={copy.outputComponent.placeholderLabel}
                    onChange={v => updateAt(index, { label: v })}
                  />
                </div>
                <div className="module-studio__field">
                  <span>{copy.outputComponent.fieldType}</span>
                  <Select
                    value={row.type}
                    disabled={disabled}
                    optionList={outputTypeOptions}
                    onChange={v => updateAt(index, { type: v as AppOutputComponent["type"] })}
                  />
                </div>
                <div className="module-studio__field module-studio__field--full">
                  <span>{copy.outputComponent.fieldSourceExpression}</span>
                  <Input
                    value={row.sourceExpression}
                    disabled={disabled}
                    placeholder={copy.outputComponent.placeholderSourceExpression}
                    onChange={v => updateAt(index, { sourceExpression: v })}
                  />
                </div>
              </div>
              <div className="module-studio__app-builder-row-actions">
                <Button icon={<IconDelete />} type="danger" theme="borderless" disabled={disabled} onClick={() => removeAt(index)}>
                  {copy.outputComponent.removeRow}
                </Button>
              </div>
            </div>
          ))
        )}
      </div>
      <Button icon={<IconPlus />} theme="light" disabled={disabled} onClick={addRow} block>
        {copy.outputComponent.addRow}
      </Button>
    </div>
  );
}

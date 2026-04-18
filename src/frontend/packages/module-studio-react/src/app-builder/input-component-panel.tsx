import { Button, Input, Select, Space, Switch, Typography } from "@douyinfe/semi-ui";
import { IconDelete, IconPlus } from "@douyinfe/semi-icons";
import type { AppInputComponent, StudioLocale } from "../types";
import { createEmptyInput } from "./app-builder-helpers";
import { getStudioCopy } from "../copy";

export interface InputComponentPanelProps {
  value: AppInputComponent[];
  onChange: (next: AppInputComponent[]) => void;
  disabled?: boolean;
  locale: StudioLocale;
}

export function InputComponentPanel({ value, onChange, disabled, locale }: InputComponentPanelProps) {
  const copy = getStudioCopy(locale);

  const inputTypeOptions: Array<{ label: string; value: AppInputComponent["type"] }> = [
    { label: copy.inputComponent.typeText, value: "text" },
    { label: copy.inputComponent.typeTextarea, value: "textarea" },
    { label: copy.inputComponent.typeNumber, value: "number" },
    { label: copy.inputComponent.typeDate, value: "date" },
    { label: copy.inputComponent.typeSelect, value: "select" },
    { label: copy.inputComponent.typeFile, value: "file" }
  ];

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
        <span>{copy.inputComponent.cardTitle}</span>
        <Typography.Text type="tertiary" size="small">
          {value.length} {copy.inputComponent.itemSuffix}
        </Typography.Text>
      </div>
      <Typography.Text type="tertiary" size="small" style={{ display: "block", marginTop: 0 }}>
        {copy.inputComponent.bodyHint}
      </Typography.Text>
      <div className="module-studio__app-builder-array">
        {value.length === 0 ? (
          <Typography.Text type="tertiary">{copy.inputComponent.emptyHint}</Typography.Text>
        ) : (
          value.map((row, index) => (
            <div key={row.id} className="module-studio__app-builder-row">
              <div className="module-studio__form-grid">
                <div className="module-studio__field">
                  <span>{copy.inputComponent.fieldLabel}</span>
                  <Input
                    value={row.label}
                    disabled={disabled}
                    placeholder={copy.inputComponent.placeholderLabel}
                    onChange={v => updateAt(index, { label: v })}
                  />
                </div>
                <div className="module-studio__field">
                  <span>{copy.inputComponent.fieldVariableKey}</span>
                  <Input
                    value={row.variableKey}
                    disabled={disabled}
                    placeholder={copy.inputComponent.placeholderVariableKey}
                    onChange={v => updateAt(index, { variableKey: v })}
                  />
                </div>
                <div className="module-studio__field">
                  <span>{copy.inputComponent.fieldType}</span>
                  <Select
                    value={row.type}
                    disabled={disabled}
                    optionList={inputTypeOptions}
                    onChange={v => {
                      const nextType = v as AppInputComponent["type"];
                      updateAt(index, {
                        type: nextType,
                        options: nextType === "select"
                          ? row.options?.length
                            ? row.options
                            : [{ label: copy.inputComponent.defaultOptionLabel, value: "a" }]
                          : undefined
                      });
                    }}
                  />
                </div>
                <div className="module-studio__field module-studio__switch-item">
                  <span>{copy.inputComponent.fieldRequired}</span>
                  <Switch
                    checked={row.required}
                    disabled={disabled}
                    onChange={checked => updateAt(index, { required: checked })}
                  />
                </div>
                {row.type === "select" ? (
                  <div className="module-studio__field module-studio__field--full">
                    <span>{copy.inputComponent.fieldOptions}</span>
                    <div className="module-studio__stack">
                      {(row.options ?? []).map((opt, optIndex) => (
                        <Space key={`${row.id}-opt-${optIndex}`} wrap>
                          <Input
                            value={opt.label}
                            disabled={disabled}
                            placeholder={copy.inputComponent.placeholderOptionLabel}
                            onChange={v => {
                              const options = [...(row.options ?? [])];
                              options[optIndex] = { ...options[optIndex], label: v };
                              updateAt(index, { options });
                            }}
                          />
                          <Input
                            value={opt.value}
                            disabled={disabled}
                            placeholder={copy.inputComponent.placeholderOptionValue}
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
                        {copy.inputComponent.addOption}
                      </Button>
                    </div>
                  </div>
                ) : null}
                <div className="module-studio__field module-studio__field--full">
                  <span>{copy.inputComponent.fieldDefault}</span>
                  <Input
                    value={row.defaultValue ?? ""}
                    disabled={disabled}
                    placeholder={copy.inputComponent.placeholderDefault}
                    onChange={v => updateAt(index, { defaultValue: v || undefined })}
                  />
                </div>
              </div>
              <div className="module-studio__app-builder-row-actions">
                <Button icon={<IconDelete />} type="danger" theme="borderless" disabled={disabled} onClick={() => removeAt(index)}>
                  {copy.inputComponent.removeRow}
                </Button>
              </div>
            </div>
          ))
        )}
      </div>
      <Button icon={<IconPlus />} theme="light" disabled={disabled} onClick={addRow} block>
        {copy.inputComponent.addRow}
      </Button>
    </div>
  );
}

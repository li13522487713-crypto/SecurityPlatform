import { Input, InputNumber, Select, Space, Switch, Typography } from "@douyinfe/semi-ui";
import { DEFAULT_PARSING_STRATEGY } from "../types";
import type {
  KnowledgeBaseKind,
  ParsingStrategy,
  SupportedLocale
} from "../types";
import { getLibraryCopy } from "../copy";

export interface ParsingStrategyFormProps {
  locale: SupportedLocale;
  kind: KnowledgeBaseKind;
  value?: ParsingStrategy;
  onChange: (value: ParsingStrategy) => void;
}

/**
 * 类型化解析策略表单：
 * - text 知识库：仅显示 parsingType / extractTable / extractImage / imageOcr / filterPages
 * - table 知识库：增加 sheetId / headerLine / dataStartLine / rowsCount
 * - image 知识库：保留 captionType；强制 extractImage=true
 *
 * Form 是受控组件，初始值缺失时使用 DEFAULT_PARSING_STRATEGY。
 */
export function ParsingStrategyForm({ locale, kind, value, onChange }: ParsingStrategyFormProps) {
  const copy = getLibraryCopy(locale);
  const strategy: ParsingStrategy = value ?? {
    ...DEFAULT_PARSING_STRATEGY,
    extractImage: kind === "image",
    captionType: kind === "image" ? "auto-vlm" : undefined
  };

  function patch(next: Partial<ParsingStrategy>): void {
    onChange({ ...strategy, ...next });
  }

  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Typography.Text strong>{copy.parsingFormParsingType}</Typography.Text>
      <Select
        value={strategy.parsingType}
        style={{ width: "100%" }}
        onChange={value => patch({ parsingType: value as ParsingStrategy["parsingType"] })}
        optionList={[
          { label: copy.parsingFormParsingTypeQuick, value: "quick" },
          { label: copy.parsingFormParsingTypePrecise, value: "precise" }
        ]}
      />

      {kind === "text" ? (
        <>
          <Typography.Text strong>{copy.parsingFormExtractImage}</Typography.Text>
          <Switch checked={strategy.extractImage} onChange={value => patch({ extractImage: value })} />
          <Typography.Text strong>{copy.parsingFormExtractTable}</Typography.Text>
          <Switch checked={strategy.extractTable} onChange={value => patch({ extractTable: value })} />
          <Typography.Text strong>{copy.parsingFormImageOcr}</Typography.Text>
          <Switch checked={strategy.imageOcr} onChange={value => patch({ imageOcr: value })} />
          <Typography.Text strong>{copy.parsingFormFilterPages}</Typography.Text>
          <Input
            value={strategy.filterPages ?? ""}
            placeholder="1-5,8,12-"
            onChange={value => patch({ filterPages: value || undefined })}
          />
        </>
      ) : null}

      {kind === "table" ? (
        <>
          <Typography.Text strong>{copy.parsingFormSheetId}</Typography.Text>
          <Input value={strategy.sheetId ?? ""} onChange={value => patch({ sheetId: value || undefined })} />
          <Typography.Text strong>{copy.parsingFormHeaderLine}</Typography.Text>
          <InputNumber
            style={{ width: "100%" }}
            min={1}
            value={strategy.headerLine ?? 1}
            onChange={value => patch({ headerLine: Math.max(1, Number(value) || 1) })}
          />
          <Typography.Text strong>{copy.parsingFormDataStartLine}</Typography.Text>
          <InputNumber
            style={{ width: "100%" }}
            min={1}
            value={strategy.dataStartLine ?? 2}
            onChange={value => patch({ dataStartLine: Math.max(1, Number(value) || 2) })}
          />
          <Typography.Text strong>{copy.parsingFormRowsCount}</Typography.Text>
          <InputNumber
            style={{ width: "100%" }}
            min={0}
            value={strategy.rowsCount ?? 0}
            onChange={value => patch({ rowsCount: Math.max(0, Number(value) || 0) })}
          />
        </>
      ) : null}

      {kind === "image" ? (
        <>
          <Typography.Text strong>{copy.parsingFormImageOcr}</Typography.Text>
          <Switch checked={strategy.imageOcr} onChange={value => patch({ imageOcr: value })} />
          <Typography.Text strong>{copy.parsingFormCaptionType}</Typography.Text>
          <Select
            value={strategy.captionType ?? "auto-vlm"}
            style={{ width: "100%" }}
            onChange={value => patch({ captionType: value as ParsingStrategy["captionType"] })}
            optionList={[
              { label: copy.parsingFormCaptionAuto, value: "auto-vlm" },
              { label: copy.parsingFormCaptionManual, value: "manual" },
              { label: copy.parsingFormCaptionFilename, value: "filename" }
            ]}
          />
        </>
      ) : null}
    </Space>
  );
}

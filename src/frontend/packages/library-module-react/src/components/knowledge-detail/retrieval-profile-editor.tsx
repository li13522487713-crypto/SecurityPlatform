import { useEffect, useState } from "react";
import {
  Banner,
  Button,
  InputNumber,
  Select,
  Space,
  Switch,
  Typography
} from "@douyinfe/semi-ui";
import {
  DEFAULT_RETRIEVAL_PROFILE,
  type KnowledgeBaseDto,
  type LibraryKnowledgeApi,
  type RetrievalProfile,
  type SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";

const RERANK_MODEL_OPTIONS = [
  { value: "", label: "(default)" },
  { value: "bge-reranker-v2", label: "bge-reranker-v2" },
  { value: "bge-reranker-large", label: "bge-reranker-large" },
  { value: "cross-encoder-ms-marco", label: "cross-encoder/ms-marco-MiniLM-L-12-v2" },
  { value: "custom", label: "Custom..." }
];

/**
 * v5 §38 / 计划 G7+G8：通用 RetrievalProfile 字段编辑器（无 save 按钮）。
 * 复用于 RetrievalProfileEditor / WorkflowKnowledgeNodePanel / AgentKnowledgeBindingPanel。
 */
export interface RetrievalProfileFieldsProps {
  locale: SupportedLocale;
  value: RetrievalProfile;
  onChange: (next: RetrievalProfile) => void;
  showRerankModel?: boolean;
}

export function RetrievalProfileFields({ locale, value, onChange, showRerankModel = true }: RetrievalProfileFieldsProps) {
  const copy = getLibraryCopy(locale);
  const [customRerank, setCustomRerank] = useState<string>(value.rerankModel ?? "");
  const isCustom = !RERANK_MODEL_OPTIONS.some(opt => opt.value === value.rerankModel) && Boolean(value.rerankModel);

  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Typography.Text strong>{copy.retrievalProfileTopK}</Typography.Text>
      <InputNumber
        style={{ width: "100%" }}
        min={1}
        max={50}
        value={value.topK}
        onChange={v => onChange({ ...value, topK: Math.max(1, Number(v) || 1) })}
      />
      <Typography.Text strong>{copy.retrievalProfileMinScore}</Typography.Text>
      <InputNumber
        style={{ width: "100%" }}
        min={0}
        max={1}
        step={0.05}
        value={value.minScore}
        onChange={v => onChange({ ...value, minScore: Math.max(0, Math.min(1, Number(v) || 0)) })}
      />
      <Typography.Text strong>{copy.wizardEnableRerank}</Typography.Text>
      <Switch checked={value.enableRerank} onChange={v => onChange({ ...value, enableRerank: v })} />
      {showRerankModel && (
        <>
          <Typography.Text strong>RerankModel</Typography.Text>
          <Select
            style={{ width: "100%" }}
            value={isCustom ? "custom" : (value.rerankModel ?? "")}
            optionList={RERANK_MODEL_OPTIONS}
            onChange={v => {
              if (v === "custom") {
                onChange({ ...value, rerankModel: customRerank || "custom-reranker" });
              } else {
                onChange({ ...value, rerankModel: (v as string) || undefined });
              }
            }}
          />
          {(isCustom || (value.rerankModel === "" && customRerank)) && (
            <input
              type="text"
              placeholder={copy.retrievalProfileCustomRerankerPlaceholder}
              value={customRerank}
              onChange={e => {
                setCustomRerank(e.target.value);
                onChange({ ...value, rerankModel: e.target.value || undefined });
              }}
              style={{ width: "100%", padding: 6, border: "1px solid #ddd", borderRadius: 4 }}
            />
          )}
        </>
      )}
      <Typography.Text strong>{copy.wizardEnableHybrid}</Typography.Text>
      <Switch checked={value.enableHybrid} onChange={v => onChange({ ...value, enableHybrid: v })} />
      <Typography.Text strong>{copy.wizardEnableQueryRewrite}</Typography.Text>
      <Switch checked={value.enableQueryRewrite} onChange={v => onChange({ ...value, enableQueryRewrite: v })} />
      <Typography.Text strong>{copy.retrievalWeightLabel}</Typography.Text>
      <div className="atlas-summary-grid" style={{ width: "100%" }}>
        {(["vector", "bm25", "table", "image"] as const).map(key => (
          <div key={key} className="atlas-summary-tile">
            <span>{key}</span>
            <InputNumber
              style={{ width: "100%" }}
              min={0}
              max={1}
              step={0.05}
              value={value.weights[key]}
              onChange={v => onChange({
                ...value,
                weights: { ...value.weights, [key]: Math.max(0, Math.min(1, Number(v) || 0)) }
              })}
            />
          </div>
        ))}
      </div>
    </Space>
  );
}

export interface RetrievalProfileEditorProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
  onUpdated: (next: RetrievalProfile) => void;
}

export function RetrievalProfileEditor({ api, locale, knowledge, onUpdated }: RetrievalProfileEditorProps) {
  const copy = getLibraryCopy(locale);
  const [profile, setProfile] = useState<RetrievalProfile>(knowledge.retrievalProfile ?? DEFAULT_RETRIEVAL_PROFILE);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setProfile(knowledge.retrievalProfile ?? DEFAULT_RETRIEVAL_PROFILE);
  }, [knowledge.retrievalProfile]);

  async function handleSave() {
    if (!api.updateRetrievalProfile) {
      return;
    }
    setBusy(true);
    try {
      await api.updateRetrievalProfile(knowledge.id, profile);
      onUpdated(profile);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="atlas-summary-card semi-card semi-card-bordered">
      <div className="semi-card-body">
        <Typography.Title heading={6}>{copy.retrievalProfileTitle}</Typography.Title>
        <Banner type="info" description={copy.retrievalProfileTitle} />
        <div style={{ marginTop: 12 }}>
          <RetrievalProfileFields locale={locale} value={profile} onChange={setProfile} />
        </div>
        <Button type="primary" loading={busy} onClick={handleSave} style={{ marginTop: 12 }}>
          {copy.retrievalProfileSave}
        </Button>
      </div>
    </div>
  );
}

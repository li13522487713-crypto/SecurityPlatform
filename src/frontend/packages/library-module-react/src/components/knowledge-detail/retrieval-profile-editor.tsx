import { useEffect, useState } from "react";
import {
  Banner,
  Button,
  InputNumber,
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
        <Space vertical align="start" style={{ width: "100%", marginTop: 12 }}>
          <Typography.Text strong>{copy.retrievalProfileTopK}</Typography.Text>
          <InputNumber
            style={{ width: "100%" }}
            min={1}
            max={50}
            value={profile.topK}
            onChange={value => setProfile(prev => ({ ...prev, topK: Math.max(1, Number(value) || 1) }))}
          />
          <Typography.Text strong>{copy.retrievalProfileMinScore}</Typography.Text>
          <InputNumber
            style={{ width: "100%" }}
            min={0}
            max={1}
            step={0.05}
            value={profile.minScore}
            onChange={value => setProfile(prev => ({ ...prev, minScore: Math.max(0, Math.min(1, Number(value) || 0)) }))}
          />
          <Typography.Text strong>{copy.wizardEnableRerank}</Typography.Text>
          <Switch checked={profile.enableRerank} onChange={value => setProfile(prev => ({ ...prev, enableRerank: value }))} />
          <Typography.Text strong>{copy.wizardEnableHybrid}</Typography.Text>
          <Switch checked={profile.enableHybrid} onChange={value => setProfile(prev => ({ ...prev, enableHybrid: value }))} />
          <Typography.Text strong>{copy.wizardEnableQueryRewrite}</Typography.Text>
          <Switch checked={profile.enableQueryRewrite} onChange={value => setProfile(prev => ({ ...prev, enableQueryRewrite: value }))} />
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
                  value={profile.weights[key]}
                  onChange={value => setProfile(prev => ({
                    ...prev,
                    weights: { ...prev.weights, [key]: Math.max(0, Math.min(1, Number(value) || 0)) }
                  }))}
                />
              </div>
            ))}
          </div>
          <Button type="primary" loading={busy} onClick={handleSave}>{copy.retrievalProfileSave}</Button>
        </Space>
      </div>
    </div>
  );
}

import { useEffect, useState } from "react";
import {
  Banner,
  Button,
  Input,
  InputNumber,
  Select,
  Space,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import {
  DEFAULT_CHUNKING_PROFILE,
  type ChunkingProfile,
  type KnowledgeBaseDto,
  type LibraryKnowledgeApi,
  type SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";

export interface ChunkingProfileEditorProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
  onUpdated: (next: ChunkingProfile) => void;
}

export function ChunkingProfileEditor({ api, locale, knowledge, onUpdated }: ChunkingProfileEditorProps) {
  const copy = getLibraryCopy(locale);
  const [profile, setProfile] = useState<ChunkingProfile>(knowledge.chunkingProfile ?? DEFAULT_CHUNKING_PROFILE);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setProfile(knowledge.chunkingProfile ?? DEFAULT_CHUNKING_PROFILE);
  }, [knowledge.chunkingProfile]);

  async function handleSave() {
    if (!api.updateChunkingProfile) {
      Toast.warning(copy.chunkingProfileSave);
      return;
    }
    setBusy(true);
    try {
      await api.updateChunkingProfile(knowledge.id, profile);
      onUpdated(profile);
      Toast.success(copy.chunkingProfileSave);
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="atlas-summary-card semi-card semi-card-bordered">
      <div className="semi-card-body">
        <Typography.Title heading={6}>{copy.chunkingProfileTitle}</Typography.Title>
        <Banner type="info" description={copy.chunkingProfileTitle} />
        <Space vertical align="start" style={{ width: "100%", marginTop: 12 }}>
          <Typography.Text strong>{copy.chunkingProfileMode}</Typography.Text>
          <Select
            value={profile.mode}
            style={{ width: "100%" }}
            onChange={value => setProfile(prev => ({ ...prev, mode: value as ChunkingProfile["mode"] }))}
            optionList={[
              { label: copy.chunkingProfileModeFixed, value: "fixed" },
              { label: copy.chunkingProfileModeSemantic, value: "semantic" },
              { label: copy.chunkingProfileModeTableRow, value: "table-row" },
              { label: copy.chunkingProfileModeImageItem, value: "image-item" }
            ]}
          />
          <Typography.Text strong>{copy.chunkingProfileSize}</Typography.Text>
          <InputNumber
            style={{ width: "100%" }}
            min={1}
            max={4096}
            value={profile.size}
            onChange={value => setProfile(prev => ({ ...prev, size: Number(value) || 1 }))}
          />
          <Typography.Text strong>{copy.chunkingProfileOverlap}</Typography.Text>
          <InputNumber
            style={{ width: "100%" }}
            min={0}
            max={1024}
            value={profile.overlap}
            onChange={value => setProfile(prev => ({ ...prev, overlap: Number(value) || 0 }))}
          />
          <Typography.Text strong>{copy.chunkingProfileSeparators}</Typography.Text>
          <Input
            value={profile.separators.join(",")}
            onChange={value => setProfile(prev => ({ ...prev, separators: value.split(",").map(s => s.trim()).filter(Boolean) }))}
          />
          {profile.mode === "table-row" ? (
            <>
              <Typography.Text strong>{copy.chunkingProfileIndexColumns}</Typography.Text>
              <Input
                value={(profile.indexColumns ?? []).join(",")}
                onChange={value => setProfile(prev => ({ ...prev, indexColumns: value.split(",").map(s => s.trim()).filter(Boolean) }))}
              />
            </>
          ) : null}
          <Button type="primary" loading={busy} onClick={handleSave}>{copy.chunkingProfileSave}</Button>
        </Space>
      </div>
    </div>
  );
}

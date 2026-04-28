import { useMemo, useState } from "react";
import {
  Banner,
  Button,
  Card,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Steps,
  Switch,
  TagInput,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import { IconImage, IconList, IconSafe } from "@douyinfe/semi-icons";
import {
  DEFAULT_CHUNKING_PROFILE,
  DEFAULT_RETRIEVAL_PROFILE
} from "../types";
import type {
  ChunkingProfile,
  KnowledgeBaseCreateRequest,
  KnowledgeBaseKind,
  LibraryKnowledgeApi,
  RetrievalProfile,
  SupportedLocale
} from "../types";
import { getLibraryCopy } from "../copy";

export interface KnowledgeBaseCreateWizardProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  visible: boolean;
  initialKind?: KnowledgeBaseKind;
  onCreated: (knowledgeBaseId: number, kind: KnowledgeBaseKind) => void;
  onCancel: () => void;
}

interface WizardState {
  kind: KnowledgeBaseKind;
  name: string;
  description: string;
  tags: string[];
  providerKind: "builtin" | "qdrant" | "external";
  chunkingProfile: ChunkingProfile;
  retrievalProfile: RetrievalProfile;
}

const KIND_TO_TYPE: Record<KnowledgeBaseKind, 0 | 1 | 2> = {
  text: 0,
  table: 1,
  image: 2
};

function defaultStateFor(kind: KnowledgeBaseKind): WizardState {
  return {
    kind,
    name: "",
    description: "",
    tags: [],
    providerKind: kind === "image" ? "qdrant" : "builtin",
    chunkingProfile:
      kind === "table"
        ? { mode: "table-row", size: 1, overlap: 0, separators: ["\n"] }
        : kind === "image"
          ? { mode: "image-item", size: 1, overlap: 0, separators: [] }
          : { ...DEFAULT_CHUNKING_PROFILE },
    retrievalProfile: {
      ...DEFAULT_RETRIEVAL_PROFILE,
      weights: {
        ...DEFAULT_RETRIEVAL_PROFILE.weights,
        table: kind === "table" ? 0.6 : 0,
        image: kind === "image" ? 0.6 : 0
      }
    }
  };
}

export function KnowledgeBaseCreateWizard({
  api,
  locale,
  visible,
  initialKind,
  onCreated,
  onCancel
}: KnowledgeBaseCreateWizardProps) {
  const copy = getLibraryCopy(locale);
  const [step, setStep] = useState<number>(initialKind ? 1 : 0);
  const [state, setState] = useState<WizardState>(defaultStateFor(initialKind ?? "text"));
  const [submitting, setSubmitting] = useState(false);

  const kindCards = useMemo(() => ([
    {
      kind: "text" as const,
      icon: <IconSafe size="extra-large" />,
      title: copy.wizardKindText,
      desc: copy.wizardKindTextDesc
    },
    {
      kind: "table" as const,
      icon: <IconList size="extra-large" />,
      title: copy.wizardKindTable,
      desc: copy.wizardKindTableDesc
    },
    {
      kind: "image" as const,
      icon: <IconImage size="extra-large" />,
      title: copy.wizardKindImage,
      desc: copy.wizardKindImageDesc
    }
  ]), [copy]);

  function reset() {
    setStep(initialKind ? 1 : 0);
    setState(defaultStateFor(initialKind ?? "text"));
    setSubmitting(false);
  }

  function handleCancel() {
    reset();
    onCancel();
  }

  async function handleFinish() {
    if (!state.name.trim()) {
      Toast.warning(copy.wizardValidationName);
      setStep(1);
      return;
    }
    setSubmitting(true);
    try {
      const request: KnowledgeBaseCreateRequest = {
        name: state.name.trim(),
        description: state.description.trim() || undefined,
        type: KIND_TO_TYPE[state.kind],
        kind: state.kind,
        providerKind: state.providerKind,
        providerConfigId:
          state.providerKind === "qdrant"
            ? "vector-qdrant-default"
            : state.providerKind === "builtin"
              ? "vector-sqlite-default"
              : undefined,
        chunkingProfile: state.chunkingProfile,
        retrievalProfile: state.retrievalProfile,
        tags: state.tags
      };
      const id = await api.createKnowledgeBase(request);
      Toast.success(copy.wizardCreateSuccess);
      onCreated(id, state.kind);
      reset();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal
      title={copy.createKnowledge}
      visible={visible}
      onCancel={handleCancel}
      width={760}
      footer={(
        <Space>
          {step > 0 ? (
            <Button onClick={() => setStep(prev => Math.max(0, prev - 1))}>{copy.wizardBack}</Button>
          ) : null}
          {step < 2 ? (
            <Button
              type="primary"
              disabled={step === 0 ? false : !state.name.trim()}
              onClick={() => setStep(prev => Math.min(2, prev + 1))}
            >
              {copy.wizardNext}
            </Button>
          ) : (
            <Button type="primary" loading={submitting} onClick={handleFinish}>
              {copy.wizardFinish}
            </Button>
          )}
        </Space>
      )}
    >
      <Steps current={step} type="basic" size="small" style={{ marginBottom: 16 }}>
        <Steps.Step title={copy.wizardChooseKind} />
        <Steps.Step title={copy.wizardBasicInfo} />
        <Steps.Step title={copy.wizardConfigureProfile} />
      </Steps>

      {step === 0 ? (
        <div className="atlas-kb-wizard-kind-grid">
          {kindCards.map(card => (
            <Card
              key={card.kind}
              className={state.kind === card.kind ? "atlas-kb-wizard-kind-card atlas-kb-wizard-kind-card--active" : "atlas-kb-wizard-kind-card"}
              onClick={() => {
                setState(defaultStateFor(card.kind));
                setStep(1);
              }}
            >
              <div className="atlas-kb-wizard-kind-card__icon">{card.icon}</div>
              <Typography.Title heading={5} style={{ marginTop: 8 }}>
                {card.title}
              </Typography.Title>
              <Typography.Text type="tertiary">{card.desc}</Typography.Text>
            </Card>
          ))}
        </div>
      ) : null}

      {step === 1 ? (
        <Space vertical align="start" style={{ width: "100%" }}>
          <Typography.Text strong>{copy.wizardName}</Typography.Text>
          <Input value={state.name} onChange={value => setState(prev => ({ ...prev, name: value }))} />
          <Typography.Text strong>{copy.wizardDescription}</Typography.Text>
          <Input value={state.description} onChange={value => setState(prev => ({ ...prev, description: value }))} />
          <Typography.Text strong>{copy.wizardTagsLabel}</Typography.Text>
          <Typography.Text type="tertiary" size="small">{copy.wizardTagsHint}</Typography.Text>
          <TagInput
            value={state.tags}
            onChange={value => setState(prev => ({ ...prev, tags: value as string[] }))}
            placeholder={copy.wizardTagsLabel}
            allowDuplicates={false}
          />
          <Typography.Text strong>{copy.wizardProviderLabel}</Typography.Text>
          <Select
            value={state.providerKind}
            style={{ width: "100%" }}
            onChange={value => setState(prev => ({ ...prev, providerKind: value as WizardState["providerKind"] }))}
            optionList={[
              { label: copy.wizardProviderBuiltin, value: "builtin" },
              { label: copy.wizardProviderQdrant, value: "qdrant" },
              { label: copy.wizardProviderExternal, value: "external" }
            ]}
          />
        </Space>
      ) : null}

      {step === 2 ? (
        <Space vertical align="start" style={{ width: "100%" }}>
          <Banner type="info" description={copy.chunkingProfileTitle} />
          <div className="atlas-kb-wizard-grid">
            <div>
              <Typography.Text strong>{copy.chunkingProfileMode}</Typography.Text>
              <Select
                value={state.chunkingProfile.mode}
                style={{ width: "100%" }}
                onChange={value => setState(prev => ({
                  ...prev,
                  chunkingProfile: { ...prev.chunkingProfile, mode: value as ChunkingProfile["mode"] }
                }))}
                optionList={[
                  { label: copy.chunkingProfileModeFixed, value: "fixed" },
                  { label: copy.chunkingProfileModeSemantic, value: "semantic" },
                  { label: copy.chunkingProfileModeTableRow, value: "table-row" },
                  { label: copy.chunkingProfileModeImageItem, value: "image-item" }
                ]}
              />
            </div>
            <div>
              <Typography.Text strong>{copy.chunkingProfileSize}</Typography.Text>
              <InputNumber
                style={{ width: "100%" }}
                min={32}
                max={4096}
                value={state.chunkingProfile.size}
                onChange={value => setState(prev => ({
                  ...prev,
                  chunkingProfile: { ...prev.chunkingProfile, size: Number(value) || 0 }
                }))}
              />
            </div>
            <div>
              <Typography.Text strong>{copy.chunkingProfileOverlap}</Typography.Text>
              <InputNumber
                style={{ width: "100%" }}
                min={0}
                max={1024}
                value={state.chunkingProfile.overlap}
                onChange={value => setState(prev => ({
                  ...prev,
                  chunkingProfile: { ...prev.chunkingProfile, overlap: Number(value) || 0 }
                }))}
              />
            </div>
            <div>
              <Typography.Text strong>{copy.chunkingProfileSeparators}</Typography.Text>
              <Input
                value={state.chunkingProfile.separators.join(",")}
                onChange={value => setState(prev => ({
                  ...prev,
                  chunkingProfile: { ...prev.chunkingProfile, separators: value.split(",").map(s => s.trim()).filter(Boolean) }
                }))}
              />
            </div>
          </div>

          <Banner type="info" description={copy.retrievalProfileTitle} />
          <div className="atlas-kb-wizard-grid">
            <div>
              <Typography.Text strong>{copy.wizardTopK}</Typography.Text>
              <InputNumber
                style={{ width: "100%" }}
                min={1}
                max={50}
                value={state.retrievalProfile.topK}
                onChange={value => setState(prev => ({
                  ...prev,
                  retrievalProfile: { ...prev.retrievalProfile, topK: Number(value) || 1 }
                }))}
              />
            </div>
            <div>
              <Typography.Text strong>{copy.wizardEnableRerank}</Typography.Text>
              <Switch
                checked={state.retrievalProfile.enableRerank}
                onChange={value => setState(prev => ({
                  ...prev,
                  retrievalProfile: { ...prev.retrievalProfile, enableRerank: value }
                }))}
              />
            </div>
            <div>
              <Typography.Text strong>{copy.wizardEnableHybrid}</Typography.Text>
              <Switch
                checked={state.retrievalProfile.enableHybrid}
                onChange={value => setState(prev => ({
                  ...prev,
                  retrievalProfile: { ...prev.retrievalProfile, enableHybrid: value }
                }))}
              />
            </div>
            <div>
              <Typography.Text strong>{copy.wizardEnableQueryRewrite}</Typography.Text>
              <Switch
                checked={state.retrievalProfile.enableQueryRewrite}
                onChange={value => setState(prev => ({
                  ...prev,
                  retrievalProfile: { ...prev.retrievalProfile, enableQueryRewrite: value }
                }))}
              />
            </div>
          </div>
        </Space>
      ) : null}
    </Modal>
  );
}

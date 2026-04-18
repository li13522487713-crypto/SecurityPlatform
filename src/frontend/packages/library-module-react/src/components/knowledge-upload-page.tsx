import { useEffect, useMemo, useRef, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Progress,
  Space,
  Steps,
  Tag,
  TextArea,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import { IconArrowLeft, IconPlus, IconUpload } from "@douyinfe/semi-icons";
import {
  DEFAULT_PARSING_STRATEGY,
  type KnowledgeBaseDto,
  type KnowledgeBaseKind,
  type KnowledgeUploadPageProps,
  type ParsingStrategy
} from "../types";
import { getLibraryCopy } from "../copy";
import { ParsingStrategyForm } from "./parsing-strategy-form";
import { KnowledgeStateBadge } from "./knowledge-state-badge";

interface UploadTask {
  fileName: string;
  documentId?: number;
  status: "queued" | "processing" | "done" | "failed";
  message?: string;
  progress: number;
  lifecycle?: "Draft" | "Uploading" | "Uploaded" | "Parsing" | "Chunking" | "Indexing" | "Ready" | "Failed" | "Archived";
}

function deriveKind(initialType: string | null | undefined, kbKind: KnowledgeBaseKind | undefined): KnowledgeBaseKind {
  if (kbKind) return kbKind;
  if (initialType === "table") return "table";
  if (initialType === "image") return "image";
  return "text";
}

export function KnowledgeUploadPage({
  api,
  locale,
  appKey,
  knowledgeBaseId,
  initialType,
  onNavigate
}: KnowledgeUploadPageProps) {
  const copy = getLibraryCopy(locale);
  const [knowledge, setKnowledge] = useState<KnowledgeBaseDto | null>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [tasks, setTasks] = useState<UploadTask[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [tagsJson, setTagsJson] = useState("");
  const [imageMetadataJson, setImageMetadataJson] = useState("");
  const [step, setStep] = useState<number>(0);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const kind = deriveKind(initialType, knowledge?.kind);
  const [parsingStrategy, setParsingStrategy] = useState<ParsingStrategy>({
    ...DEFAULT_PARSING_STRATEGY,
    extractImage: kind === "image",
    captionType: kind === "image" ? "auto-vlm" : undefined
  });

  useEffect(() => {
    let disposed = false;
    void api.getKnowledgeBase(knowledgeBaseId).then(result => {
      if (!disposed) {
        setKnowledge(result);
        // 根据 KB kind 调整默认解析策略
        if (result.kind === "image") {
          setParsingStrategy(prev => ({ ...prev, extractImage: true, captionType: prev.captionType ?? "auto-vlm" }));
        } else if (result.kind === "table") {
          setParsingStrategy(prev => ({ ...prev, parsingType: "precise", extractTable: true, sheetId: prev.sheetId ?? "Sheet1", headerLine: prev.headerLine ?? 1, dataStartLine: prev.dataStartLine ?? 2 }));
        }
      }
    }).catch(error => {
      if (!disposed) {
        Toast.error((error as Error).message);
      }
    });
    return () => {
      disposed = true;
    };
  }, [api, knowledgeBaseId]);

  // 订阅 mock scheduler，实时把任务进度回写到 UI
  useEffect(() => {
    if (!api.subscribeJobs) return undefined;
    return api.subscribeJobs(knowledgeBaseId, job => {
      if (!job.documentId) return;
      setTasks(current => current.map(task => {
        if (task.documentId !== job.documentId) return task;
        const lifecycle = job.status === "Succeeded"
          ? "Ready"
          : job.status === "Failed" || job.status === "DeadLetter"
            ? "Failed"
            : job.type === "parse"
              ? "Parsing"
              : job.type === "index"
                ? "Indexing"
                : "Chunking";
        return {
          ...task,
          lifecycle,
          progress: Math.max(task.progress, job.progress),
          status: job.status === "Succeeded" ? "done" : job.status === "Failed" || job.status === "DeadLetter" ? "failed" : "processing",
          message: job.errorMessage
        };
      }));
    });
  }, [api, knowledgeBaseId]);

  const typeLabel = useMemo(() => {
    if (kind === "table") return copy.typeLabels[1];
    if (kind === "image") return copy.typeLabels[2];
    return copy.typeLabels[0];
  }, [copy, kind]);

  function buildOptions(): { ok: true; value?: { tagsJson?: string; imageMetadataJson?: string; parsingStrategy?: ParsingStrategy } } | { ok: false } {
    const tagsTrim = tagsJson.trim();
    if (tagsTrim.length > 0) {
      try {
        const parsed: unknown = JSON.parse(tagsTrim);
        if (!Array.isArray(parsed)) return { ok: false };
      } catch {
        return { ok: false };
      }
    }
    const metaTrim = imageMetadataJson.trim();
    if (kind === "image" && metaTrim.length > 0) {
      try {
        const parsed: unknown = JSON.parse(metaTrim);
        if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) return { ok: false };
      } catch {
        return { ok: false };
      }
    }
    const opt: { tagsJson?: string; imageMetadataJson?: string; parsingStrategy?: ParsingStrategy } = {
      parsingStrategy
    };
    if (tagsTrim.length > 0) opt.tagsJson = tagsTrim;
    if (kind === "image" && metaTrim.length > 0) opt.imageMetadataJson = metaTrim;
    return { ok: true, value: opt };
  }

  async function handleSubmit() {
    if (files.length === 0) {
      Toast.warning(copy.uploadEmpty);
      return;
    }
    const built = buildOptions();
    if (!built.ok) {
      Toast.error(copy.uploadTagsInvalid);
      return;
    }
    const uploadOptions = built.value;

    setSubmitting(true);
    setStep(3);
    setTasks(files.map(file => ({
      fileName: file.name,
      status: "queued",
      progress: 0,
      lifecycle: "Uploading"
    })));

    try {
      for (const file of files) {
        setTasks(current => current.map(task => (
          task.fileName === file.name
            ? { ...task, status: "processing", progress: 10, lifecycle: "Uploading" }
            : task
        )));
        const documentId = await api.uploadDocument(knowledgeBaseId, file, uploadOptions);
        setTasks(current => current.map(task => (
          task.fileName === file.name
            ? { ...task, documentId, status: "processing", progress: 30, lifecycle: "Uploaded" }
            : task
        )));
        // 真实 API：fallback 老式 polling；mock：上面的 subscribeJobs 会推进
        if (!api.subscribeJobs) {
          await pollDocument(documentId, file.name);
        }
      }
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  async function pollDocument(documentId: number, fileName: string) {
    for (let attempt = 0; attempt < 60; attempt += 1) {
      const progress = await api.getDocumentProgress(knowledgeBaseId, documentId);
      const percent = progress.status === 2 ? 100 : progress.status === 3 ? 100 : Math.min(92, 30 + attempt * 3);
      setTasks(current => current.map(task => (
        task.documentId === documentId
          ? {
              ...task,
              progress: percent,
              status: progress.status === 2 ? "done" : progress.status === 3 ? "failed" : "processing",
              lifecycle: progress.lifecycleStatus,
              message: progress.errorMessage
            }
          : task
      )));
      if (progress.status === 2 || progress.status === 3) return;
      await new Promise(resolve => window.setTimeout(resolve, 1500));
    }
    setTasks(current => current.map(task => (
      task.documentId === documentId
        ? { ...task, status: "failed", progress: 100, message: copy.uploadFailed, lifecycle: "Failed" }
        : task
    )));
    Toast.error(`${fileName}: ${copy.uploadFailed}`);
  }

  return (
    <div className="atlas-library-page" data-testid="app-knowledge-upload-page">
      <div className="atlas-page-header">
        <Space spacing={8}>
          <Button icon={<IconArrowLeft />} onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${knowledgeBaseId}`)}>
            {copy.backToLibrary}
          </Button>
          <div>
            <Typography.Title heading={3} style={{ margin: 0 }}>{copy.uploadTitle}</Typography.Title>
            <Typography.Text type="tertiary">{knowledge?.name ?? copy.knowledgeBase}</Typography.Text>
          </div>
        </Space>
        <Tag color="light-blue">{typeLabel}</Tag>
      </div>

      <Steps current={step} type="basic" size="small" style={{ marginBottom: 16 }}>
        <Steps.Step title={copy.stepFile} />
        <Steps.Step title={copy.stepType} />
        <Steps.Step title={copy.stepProcessing} />
        <Steps.Step title={copy.stepComplete} />
      </Steps>

      {step === 0 ? (
        <div className="atlas-upload-step-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-body">
            <Typography.Title heading={5}>{copy.stepFile}</Typography.Title>
            <Typography.Text type="tertiary">{copy.uploadProcessingHint}</Typography.Text>
            <div className="atlas-upload-dropzone" onClick={() => fileInputRef.current?.click()}>
              <IconUpload size="extra-large" />
              <Typography.Text>{copy.uploadSelectFile}</Typography.Text>
              <Typography.Text type="tertiary">
                {files.length > 0 ? `${files.length} file(s)` : copy.uploadEmpty}
              </Typography.Text>
            </div>
            <input
              ref={fileInputRef}
              type="file"
              multiple
              style={{ display: "none" }}
              onChange={event => setFiles(Array.from(event.target.files ?? []))}
            />
            {files.length > 0 ? (
              <div className="atlas-upload-file-list">
                {files.map(file => (
                  <div key={file.name} className="atlas-upload-file-item">
                    <span>{file.name}</span>
                    <span>{Math.round(file.size / 1024)} KB</span>
                  </div>
                ))}
              </div>
            ) : (
              <Empty description={copy.uploadEmpty} />
            )}
            <Typography.Text strong style={{ display: "block", marginTop: 12 }}>{copy.uploadTagsLabel}</Typography.Text>
            <TextArea
              value={tagsJson}
              placeholder={copy.uploadTagsPlaceholder}
              rows={2}
              onChange={setTagsJson}
            />
            {kind === "image" ? (
              <>
                <Typography.Text strong style={{ display: "block", marginTop: 12 }}>{copy.uploadImageMetaLabel}</Typography.Text>
                <TextArea
                  value={imageMetadataJson}
                  placeholder={copy.uploadImageMetaPlaceholder}
                  rows={3}
                  onChange={setImageMetadataJson}
                />
              </>
            ) : null}
            <div style={{ marginTop: 16 }}>
              <Button type="primary" disabled={files.length === 0} onClick={() => setStep(1)}>
                {copy.wizardNext}
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {step === 1 ? (
        <div className="atlas-upload-step-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-body">
            <Typography.Title heading={5}>{copy.stepType}</Typography.Title>
            <Banner type="info" description={copy.uploadProcessingHint} />
            <ParsingStrategyForm
              locale={locale}
              kind={kind}
              value={parsingStrategy}
              onChange={setParsingStrategy}
            />
            <Space spacing={8} style={{ marginTop: 16 }}>
              <Button onClick={() => setStep(0)}>{copy.wizardBack}</Button>
              <Button type="primary" onClick={() => setStep(2)}>{copy.wizardNext}</Button>
            </Space>
          </div>
        </div>
      ) : null}

      {step === 2 ? (
        <div className="atlas-upload-step-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-body">
            <Typography.Title heading={5}>{copy.wizardSummary}</Typography.Title>
            <Banner type="info" description={copy.uploadProcessingHint} />
            <div className="atlas-summary-grid">
              <div className="atlas-summary-tile">
                <span>{copy.knowledgeBase}</span>
                <strong>{knowledge?.name ?? "-"}</strong>
              </div>
              <div className="atlas-summary-tile">
                <span>{copy.resourceType}</span>
                <strong>{typeLabel}</strong>
              </div>
              <div className="atlas-summary-tile">
                <span>{copy.uploadSelectFile}</span>
                <strong>{files.length}</strong>
              </div>
              <div className="atlas-summary-tile">
                <span>{copy.parsingFormParsingType}</span>
                <strong>{parsingStrategy.parsingType}</strong>
              </div>
            </div>
            <Space spacing={8} style={{ marginTop: 16 }}>
              <Button onClick={() => setStep(1)}>{copy.wizardBack}</Button>
              <Button type="primary" loading={submitting} icon={<IconPlus />} onClick={handleSubmit}>
                {copy.uploadSubmit}
              </Button>
            </Space>
          </div>
        </div>
      ) : null}

      {step === 3 ? (
        <div className="atlas-upload-status-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-body">
            <Typography.Title heading={5}>{copy.stepComplete}</Typography.Title>
            {tasks.length === 0 ? (
              <Empty description={copy.uploadEmpty} />
            ) : (
              <div className="atlas-upload-task-list">
                {tasks.map(task => (
                  <div key={task.fileName} className="atlas-upload-task">
                    <div className="atlas-upload-task__header">
                      <span>{task.fileName}</span>
                      <Space spacing={4}>
                        {task.lifecycle ? (
                          <KnowledgeStateBadge locale={locale} lifecycle={task.lifecycle} />
                        ) : null}
                        <Tag color={task.status === "done" ? "green" : task.status === "failed" ? "red" : "orange"}>
                          {task.status === "done" ? copy.uploadDone : task.status === "failed" ? copy.uploadFailed : copy.uploadProgress}
                        </Tag>
                      </Space>
                    </div>
                    <Progress percent={task.progress} showInfo />
                    {task.message ? (
                      <Typography.Text type="danger">{task.message}</Typography.Text>
                    ) : null}
                    {task.documentId ? (
                      <Typography.Text type="tertiary" size="small">
                        documentId={task.documentId}
                      </Typography.Text>
                    ) : null}
                  </div>
                ))}
              </div>
            )}
            <Space spacing={8} style={{ marginTop: 16 }}>
              <Button onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${knowledgeBaseId}?tab=jobs`)}>
                {copy.detailTabJobs}
              </Button>
              <Button
                type="primary"
                onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${knowledgeBaseId}?tab=documents`)}
              >
                {copy.detailTabDocuments}
              </Button>
            </Space>
          </div>
        </div>
      ) : null}
    </div>
  );
}

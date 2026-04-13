import { useEffect, useMemo, useRef, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Progress,
  Space,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import { IconArrowLeft, IconPlus, IconUpload } from "@douyinfe/semi-icons";
import type { KnowledgeUploadPageProps } from "../types";
import { getLibraryCopy } from "../copy";

interface UploadTask {
  fileName: string;
  documentId?: number;
  status: "queued" | "processing" | "done" | "failed";
  message?: string;
  progress: number;
}

export function KnowledgeUploadPage({
  api,
  locale,
  appKey,
  spaceId,
  knowledgeBaseId,
  initialType,
  onNavigate
}: KnowledgeUploadPageProps) {
  const copy = getLibraryCopy(locale);
  const [knowledgeName, setKnowledgeName] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [tasks, setTasks] = useState<UploadTask[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    let disposed = false;
    void api.getKnowledgeBase(knowledgeBaseId).then(result => {
      if (!disposed) {
        setKnowledgeName(result.name);
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

  const typeLabel = useMemo(() => {
    if (initialType === "table") {
      return copy.typeLabels[1];
    }
    if (initialType === "image") {
      return copy.typeLabels[2];
    }
    return copy.typeLabels[0];
  }, [copy, initialType]);

  async function pollDocument(documentId: number, fileName: string) {
    for (let attempt = 0; attempt < 60; attempt += 1) {
      const progress = await api.getDocumentProgress(knowledgeBaseId, documentId);
      const percent = progress.status === 2 ? 100 : progress.status === 3 ? 100 : Math.min(92, 20 + attempt * 3);
      setTasks(current => current.map((task: UploadTask) => (
        task.documentId === documentId
          ? {
              ...task,
              progress: percent,
              status: progress.status === 2 ? "done" : progress.status === 3 ? "failed" : "processing",
              message: progress.errorMessage
            }
          : task
      )));

      if (progress.status === 2 || progress.status === 3) {
        return;
      }

      await new Promise(resolve => window.setTimeout(resolve, 1500));
    }

    setTasks(current => current.map((task: UploadTask) => (
      task.documentId === documentId
        ? { ...task, status: "failed", progress: 100, message: copy.uploadFailed }
        : task
    )));
    Toast.error(`${fileName}: ${copy.uploadFailed}`);
  }

  async function handleSubmit() {
    if (files.length === 0) {
      Toast.warning(copy.uploadEmpty);
      return;
    }

    setSubmitting(true);
    setTasks(files.map((file: File) => ({
      fileName: file.name,
      status: "queued",
      progress: 0
    })));

    try {
      for (const file of files) {
        setTasks(current => current.map((task: UploadTask) => (
          task.fileName === file.name
            ? { ...task, status: "processing", progress: 10 }
            : task
        )));

        const documentId = await api.uploadDocument(knowledgeBaseId, file);
        setTasks(current => current.map((task: UploadTask) => (
          task.fileName === file.name
            ? { ...task, documentId, status: "processing", progress: 30 }
            : task
        )));
        await pollDocument(documentId, file.name);
      }
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="atlas-library-page" data-testid="app-knowledge-upload-page">
      <div className="atlas-page-header">
        <Space spacing={8}>
          <Button icon={<IconArrowLeft />} onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${knowledgeBaseId}`)}>
            {copy.backToLibrary}
          </Button>
          <div>
            <Typography.Title heading={3} style={{ margin: 0 }}>
              {copy.uploadTitle}
            </Typography.Title>
            <Typography.Text type="tertiary">
              {knowledgeName || copy.knowledgeBase}
            </Typography.Text>
          </div>
        </Space>
        <Tag color="light-blue">{typeLabel}</Tag>
      </div>

      <div className="atlas-upload-grid">
        <div className="atlas-upload-step-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-body">
            <Typography.Title heading={5}>{copy.stepType}</Typography.Title>
            <Typography.Text type="tertiary">{copy.uploadSubtitle}</Typography.Text>
            <div className="atlas-upload-type-pill">{typeLabel}</div>
          </div>
        </div>

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
                {files.map((file: File) => (
                  <div key={file.name} className="atlas-upload-file-item">
                    <span>{file.name}</span>
                    <span>{Math.round(file.size / 1024)} KB</span>
                  </div>
                ))}
              </div>
            ) : (
              <Empty description={copy.uploadEmpty} />
            )}
          </div>
        </div>

        <div className="atlas-upload-step-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-body">
            <Typography.Title heading={5}>{copy.stepProcessing}</Typography.Title>
            <Banner type="info" description={copy.uploadProcessingHint} />
            <div style={{ marginTop: 16 }}>
              <Button type="primary" loading={submitting} icon={<IconPlus />} onClick={handleSubmit}>
                {copy.uploadSubmit}
              </Button>
            </div>
          </div>
        </div>
      </div>

      <div className="atlas-upload-status-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body">
          <Typography.Title heading={5}>{copy.stepComplete}</Typography.Title>
          {tasks.length === 0 ? (
            <Empty description={copy.noTestResult} />
          ) : (
            <div className="atlas-upload-task-list">
              {tasks.map((task: UploadTask) => (
                <div key={task.fileName} className="atlas-upload-task">
                  <div className="atlas-upload-task__header">
                    <span>{task.fileName}</span>
                    <Tag color={task.status === "done" ? "green" : task.status === "failed" ? "red" : "orange"}>
                      {task.status === "done" ? copy.uploadDone : task.status === "failed" ? copy.uploadFailed : copy.uploadProgress}
                    </Tag>
                  </div>
                  <Progress percent={task.progress} showInfo />
                  {task.message ? (
                    <Typography.Text type="danger">{task.message}</Typography.Text>
                  ) : null}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

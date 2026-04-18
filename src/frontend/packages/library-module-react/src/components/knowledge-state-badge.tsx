import { Tag } from "@douyinfe/semi-ui";
import type { TagColor } from "@douyinfe/semi-ui/lib/es/tag";
import type { KnowledgeDocumentLifecycleStatus, KnowledgeJobStatus, SupportedLocale } from "../types";
import { getLibraryCopy } from "../copy";

const LIFECYCLE_COLOR: Record<KnowledgeDocumentLifecycleStatus, TagColor> = {
  Draft: "grey",
  Uploading: "blue",
  Uploaded: "blue",
  Parsing: "cyan",
  Chunking: "violet",
  Indexing: "orange",
  Ready: "green",
  Failed: "red",
  Archived: "white"
};

const JOB_COLOR: Record<KnowledgeJobStatus, TagColor> = {
  Queued: "grey",
  Running: "blue",
  Succeeded: "green",
  Failed: "red",
  Retrying: "orange",
  DeadLetter: "red",
  Canceled: "white"
};

export interface KnowledgeStateBadgeProps {
  locale: SupportedLocale;
  lifecycle?: KnowledgeDocumentLifecycleStatus;
  jobStatus?: KnowledgeJobStatus;
  size?: "small" | "default" | "large";
}

export function KnowledgeStateBadge({ locale, lifecycle, jobStatus, size }: KnowledgeStateBadgeProps) {
  const copy = getLibraryCopy(locale);
  if (jobStatus) {
    const labelMap: Record<KnowledgeJobStatus, string> = {
      Queued: copy.jobStatusQueued,
      Running: copy.jobStatusRunning,
      Succeeded: copy.jobStatusSucceeded,
      Failed: copy.jobStatusFailed,
      Retrying: copy.jobStatusRetrying,
      DeadLetter: copy.jobStatusDeadLetter,
      Canceled: copy.jobStatusCanceled
    };
    return (
      <Tag color={JOB_COLOR[jobStatus]} size={size ?? "small"}>
        {labelMap[jobStatus]}
      </Tag>
    );
  }
  if (!lifecycle) {
    return null;
  }
  return (
    <Tag color={LIFECYCLE_COLOR[lifecycle]} size={size ?? "small"}>
      {lifecycle}
    </Tag>
  );
}

import React from "react";
import { Tag } from "@douyinfe/semi-ui";
import type { TagColor } from "@douyinfe/semi-ui/lib/es/tag";
import type { StudioLocale } from "../types";
import { getStudioCopy } from "../copy";

export interface StatusTagProps {
  status: "draft" | "published" | "outdated" | string;
  label?: string;
  style?: React.CSSProperties;
  locale: StudioLocale;
}

export function StatusTag({ status, label, style, locale }: StatusTagProps) {
  const copy = getStudioCopy(locale);
  const normalizedStatus = status?.toLowerCase() || "draft";
  let color: TagColor = "blue";
  let defaultLabel = copy.status.unknown;

  if (normalizedStatus === "published") {
    color = "green";
    defaultLabel = copy.status.published;
  } else if (normalizedStatus === "draft") {
    color = "blue";
    defaultLabel = copy.status.draft;
  } else if (normalizedStatus === "outdated") {
    color = "orange";
    defaultLabel = copy.status.outdated;
  }

  return (
    <Tag color={color} style={style}>
      {label || defaultLabel}
    </Tag>
  );
}

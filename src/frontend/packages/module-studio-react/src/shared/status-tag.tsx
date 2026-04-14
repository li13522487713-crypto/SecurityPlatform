import React from "react";
import { Tag } from "@douyinfe/semi-ui";
import type { TagColor } from "@douyinfe/semi-ui/lib/es/tag";

export interface StatusTagProps {
  status: "draft" | "published" | "outdated" | string;
  label?: string;
  style?: React.CSSProperties;
}

export function StatusTag({ status, label, style }: StatusTagProps) {
  const normalizedStatus = status?.toLowerCase() || "draft";
  let color: TagColor = "blue";
  let defaultLabel = "未知";

  if (normalizedStatus === "published") {
    color = "green";
    defaultLabel = "已发布";
  } else if (normalizedStatus === "draft") {
    color = "blue";
    defaultLabel = "草稿";
  } else if (normalizedStatus === "outdated") {
    color = "orange";
    defaultLabel = "有更新";
  }

  return (
    <Tag color={color} style={style}>
      {label || defaultLabel}
    </Tag>
  );
}

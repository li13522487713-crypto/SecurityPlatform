import React, { type ReactNode } from "react";
import { Banner, Button, Empty, Spin } from "@douyinfe/semi-ui";
import { IconAlertTriangle } from "@douyinfe/semi-icons";
import type { PageStatus } from "./use-page-state";

export interface PageStateWrapperProps {
  status: PageStatus;
  error?: Error | null;
  onRetry?: () => void;
  emptyProps?: {
    title?: string;
    description?: ReactNode;
    image?: ReactNode;
    action?: ReactNode;
  };
  children: ReactNode;
}

export function PageStateWrapper({
  status,
  error,
  onRetry,
  emptyProps,
  children
}: PageStateWrapperProps) {
  if (status === "loading" || status === "idle") {
    return (
      <div style={{ padding: 40, display: "flex", justifyContent: "center", alignItems: "center", minHeight: 300 }}>
        <Spin size="large" />
      </div>
    );
  }

  if (status === "error") {
    return (
      <div style={{ padding: 40 }}>
        <Banner
          type="danger"
          fullMode={false}
          icon={<IconAlertTriangle />}
          title="加载数据失败"
          description={
            <div>
              <p>{error?.message || "发生未知错误"}</p>
              {onRetry && (
                <Button theme="solid" type="danger" onClick={onRetry} style={{ marginTop: 12 }}>
                  重试
                </Button>
              )}
            </div>
          }
        />
      </div>
    );
  }

  if (status === "empty") {
    return (
      <div style={{ padding: 40 }}>
        <Empty
          title={emptyProps?.title || "暂无数据"}
          description={emptyProps?.description}
          image={emptyProps?.image}
        >
          {emptyProps?.action}
        </Empty>
      </div>
    );
  }

  return <>{children}</>;
}

import React, { type ReactNode } from "react";
import { Banner, Button, Empty, Spin } from "@douyinfe/semi-ui";
import { IconAlertTriangle } from "@douyinfe/semi-icons";
import type { PageStatus } from "./use-page-state";
import type { StudioLocale } from "../types";
import { getStudioCopy } from "../copy";

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
  locale: StudioLocale;
}

export function PageStateWrapper({
  status,
  error,
  onRetry,
  emptyProps,
  children,
  locale
}: PageStateWrapperProps) {
  const copy = getStudioCopy(locale);
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
          title={copy.common.loadDataFailed}
          description={
            <div>
              <p>{error?.message || copy.common.unknownError}</p>
              {onRetry && (
                <Button theme="solid" type="danger" onClick={onRetry} style={{ marginTop: 12 }}>
                  {copy.common.retry}
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
          title={emptyProps?.title || copy.common.emptyData}
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

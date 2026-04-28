export type PublishManageTab = "analysis" | "logs" | "triggers";

export function normalizePublishManageTab(value: string | null): PublishManageTab {
  if (value === "logs" || value === "triggers") {
    return value;
  }

  return "analysis";
}

export function formatPublishTime(value?: string) {
  if (!value) {
    return "-";
  }

  const parsed = Number(value);
  if (Number.isNaN(parsed) || parsed <= 0) {
    return value;
  }

  return new Date(parsed).toLocaleString();
}

export function getPublishStatusKey(status?: number) {
  switch (status) {
    case 5:
      return "cozePublishManageStatusSuccess" as const;
    case 1:
    case 3:
      return "cozePublishManageStatusFailed" as const;
    case 0:
    case 2:
    case 4:
      return "cozePublishManageStatusPublishing" as const;
    default:
      return null;
  }
}

export function getConnectorStatusKey(status?: number) {
  switch (status) {
    case 2:
      return "cozePublishManageStatusSuccess" as const;
    case 3:
      return "cozePublishManageStatusFailed" as const;
    case 1:
      return "cozePublishManageStatusInReview" as const;
    case 0:
      return "cozePublishManageStatusPublishing" as const;
    case 4:
      return "cozePublishManageStatusDisabled" as const;
    default:
      return null;
  }
}

export function summarizeConnectors(connectors: Array<{ connector_publish_status?: number }> | undefined) {
  const all = connectors ?? [];
  const successCount = all.filter(item => item.connector_publish_status === 2).length;
  const failedCount = all.filter(item => item.connector_publish_status === 3).length;
  return {
    total: all.length,
    successCount,
    failedCount
  };
}

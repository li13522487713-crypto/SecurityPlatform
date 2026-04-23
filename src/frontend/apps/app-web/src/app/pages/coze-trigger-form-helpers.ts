export interface CozeTriggerFormValues {
  name: string;
  triggerType: string;
  configJson: string;
  enabled: boolean;
}

export function buildDefaultTriggerFormValues(): CozeTriggerFormValues {
  return {
    name: "",
    triggerType: "schedule",
    configJson: "{\"cron\":\"0 8 * * *\"}",
    enabled: true
  };
}

export function normalizeTriggerConfigJson(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return "{}";
  }

  return trimmed;
}

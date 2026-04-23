import { requestApi } from "./api-core";

export interface CozePublishTriggerItem {
  trigger_id: string;
  project_id: string;
  name: string;
  trigger_type: string;
  config_json: string;
  enabled: boolean;
  created_at: string;
  updated_at: string;
}

interface CozeDataResponse<T> {
  code?: number;
  msg?: string;
  data?: T;
}

interface CozeTriggerListPayload {
  trigger_list?: CozePublishTriggerItem[];
  total?: number;
}

interface CozeTriggerPayload {
  trigger?: CozePublishTriggerItem;
}

export async function getCozePublishTriggerList(projectId: string): Promise<CozePublishTriggerItem[]> {
  const response = await requestApi<CozeDataResponse<CozeTriggerListPayload>>(
    "/api/intelligence_api/publish/trigger_list",
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        project_id: projectId
      })
    }
  );

  if (response.code !== 0) {
    throw new Error(response.msg || "Failed to load publish triggers");
  }

  return response.data?.trigger_list ?? [];
}

export async function createCozePublishTrigger(input: {
  projectId: string;
  name?: string;
  triggerType?: string;
  configJson?: string;
  enabled?: boolean;
}): Promise<CozePublishTriggerItem | null> {
  const response = await requestApi<CozeDataResponse<CozeTriggerPayload>>(
    "/api/intelligence_api/publish/trigger_create",
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        project_id: input.projectId,
        name: input.name,
        trigger_type: input.triggerType,
        config_json: input.configJson,
        enabled: input.enabled
      })
    }
  );

  if (response.code !== 0) {
    throw new Error(response.msg || "Failed to create publish trigger");
  }

  return response.data?.trigger ?? null;
}

export async function updateCozePublishTrigger(input: {
  projectId: string;
  triggerId: string;
  name?: string;
  triggerType?: string;
  configJson?: string;
  enabled?: boolean;
}): Promise<CozePublishTriggerItem | null> {
  const response = await requestApi<CozeDataResponse<CozeTriggerPayload>>(
    "/api/intelligence_api/publish/trigger_update",
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        project_id: input.projectId,
        trigger_id: input.triggerId,
        name: input.name,
        trigger_type: input.triggerType,
        config_json: input.configJson,
        enabled: input.enabled
      })
    }
  );

  if (response.code !== 0) {
    throw new Error(response.msg || "Failed to update publish trigger");
  }

  return response.data?.trigger ?? null;
}

export async function deleteCozePublishTrigger(projectId: string, triggerId: string): Promise<void> {
  const response = await requestApi<CozeDataResponse<{ deleted?: boolean }>>(
    "/api/intelligence_api/publish/trigger_delete",
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        project_id: projectId,
        trigger_id: triggerId
      })
    }
  );

  if (response.code !== 0) {
    throw new Error(response.msg || "Failed to delete publish trigger");
  }
}

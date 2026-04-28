/**
 * 扣子资源库「卡片 / Agent Card」薄封装。创建接口尚未在宿主对前端开放时，由 UI 回退为提示。
 */
export type CreateAgentCardRequest = {
  name: string;
  description?: string;
};

export async function createAgentCard(_request: CreateAgentCardRequest): Promise<number> {
  void _request;
  throw new Error("AGENT_CARD_CREATE_NOT_AVAILABLE");
}

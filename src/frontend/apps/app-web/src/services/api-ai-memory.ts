/**
 * 资源库长期记忆条目的创建封装；后端开放写入 API 后可在此接表。
 */
export type CreateLongTermMemoryItemRequest = {
  memoryKey: string;
  content?: string;
};

export async function createLongTermMemoryItem(_request: CreateLongTermMemoryItemRequest): Promise<number> {
  void _request;
  throw new Error("LONG_TERM_MEMORY_CREATE_NOT_AVAILABLE");
}

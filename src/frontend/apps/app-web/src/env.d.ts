/* eslint-disable @typescript-eslint/no-unused-vars */
interface ImportMetaEnv {
  readonly VITE_API_BASE?: string;
  readonly VITE_APP_HOST_TARGET?: string;
  readonly VITE_APP_WEB_PORT?: string;
  readonly VITE_DEFAULT_TENANT_ID?: string;
  readonly VITE_DEFAULT_USERNAME?: string;
  readonly VITE_MICROFLOW_ADAPTER_MODE?: "mock" | "local" | "http";
  readonly VITE_MICROFLOW_API_BASE_URL?: string;
  readonly MICROFLOW_ADAPTER_MODE?: "mock" | "local" | "http";
  readonly MICROFLOW_API_BASE_URL?: string;
  /** 知识库专题：开启后 LibraryKnowledgeApi 走前端 mock 适配器（v5 §32-44 复刻阶段使用） */
  readonly VITE_LIBRARY_MOCK?: string;
  readonly [key: string]: string | undefined;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

export {};

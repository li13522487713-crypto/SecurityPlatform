/* eslint-disable @typescript-eslint/no-unused-vars */
interface ImportMetaEnv {
  readonly VITE_API_BASE?: string;
  readonly VITE_APP_HOST_TARGET?: string;
  readonly VITE_APP_RUNTIME_MODE?: string;
  readonly VITE_APP_WEB_PORT?: string;
  readonly VITE_DEFAULT_TENANT_ID?: string;
  readonly VITE_DEFAULT_USERNAME?: string;
  readonly VITE_PLATFORM_HOST_TARGET?: string;
  /** 可选：低代码 studio 壳 origin（用于跨壳全页面跳转）。未配置时走同源路径。 */
  readonly VITE_LOWCODE_STUDIO_ORIGIN?: string;
  /** 知识库专题：开启后 LibraryKnowledgeApi 走前端 mock 适配器（v5 §32-44 复刻阶段使用） */
  readonly VITE_LIBRARY_MOCK?: string;
  readonly [key: string]: string | undefined;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

export {};

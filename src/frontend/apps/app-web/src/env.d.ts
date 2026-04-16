/* eslint-disable @typescript-eslint/no-unused-vars */
interface ImportMetaEnv {
  readonly VITE_API_BASE?: string;
  readonly VITE_APP_HOST_TARGET?: string;
  readonly VITE_APP_RUNTIME_MODE?: string;
  readonly VITE_APP_WEB_PORT?: string;
  readonly VITE_DEFAULT_TENANT_ID?: string;
  readonly VITE_DEFAULT_USERNAME?: string;
  readonly VITE_PLATFORM_HOST_TARGET?: string;
  readonly [key: string]: string | undefined;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

export {};

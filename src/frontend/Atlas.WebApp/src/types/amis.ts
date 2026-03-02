import type { JsonValue } from "@/types/api";

export interface AmisFetcherConfig {
  url: string;
  method?: string;
  data?: JsonValue;
  headers?: Record<string, string>;
}

export interface AmisFetcherResult {
  data: JsonValue;
  ok: boolean;
  status: number;
  msg?: string;
}

export interface AmisEnv {
  fetcher: (config: AmisFetcherConfig) => Promise<AmisFetcherResult>;
  notify: (type: "info" | "success" | "warning" | "error", msg: string) => void;
  alert: (msg: string) => void;
  confirm: (msg: string) => Promise<boolean>;
  locale?: string;
}

export type AmisSchema = Record<string, unknown>;

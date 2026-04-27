/**
 * Development/Test only.
 * Do not import this file from production runtime paths.
 * Production should use HTTP adapters through createMicroflowAdapterBundle.
 */
import { createLocalMicroflowResourceAdapter, type LocalMicroflowResourceAdapterOptions } from "./local-microflow-resource-adapter";
import type { MicroflowResourceAdapter } from "./microflow-resource-adapter";

export function createMockMicroflowResourceAdapter(options?: LocalMicroflowResourceAdapterOptions): MicroflowResourceAdapter {
  return createLocalMicroflowResourceAdapter({ ...options, enableLocalStorage: false });
}

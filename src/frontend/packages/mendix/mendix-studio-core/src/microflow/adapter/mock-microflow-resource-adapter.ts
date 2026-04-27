import { createLocalMicroflowResourceAdapter, type LocalMicroflowResourceAdapterOptions } from "./local-microflow-resource-adapter";
import type { MicroflowResourceAdapter } from "./microflow-resource-adapter";

/**
 * dev/test/sample only. Production paths must use the http adapter through
 * createMicroflowAdapterBundle instead of importing this factory.
 */
export function createMockMicroflowResourceAdapter(options?: LocalMicroflowResourceAdapterOptions): MicroflowResourceAdapter {
  return createLocalMicroflowResourceAdapter({ ...options, enableLocalStorage: false });
}

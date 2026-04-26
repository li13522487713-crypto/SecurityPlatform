import { createLocalMicroflowResourceAdapter, type LocalMicroflowResourceAdapterOptions } from "./local-microflow-resource-adapter";
import type { MicroflowResourceAdapter } from "./microflow-resource-adapter";

export function createMockMicroflowResourceAdapter(options?: LocalMicroflowResourceAdapterOptions): MicroflowResourceAdapter {
  return createLocalMicroflowResourceAdapter(options);
}

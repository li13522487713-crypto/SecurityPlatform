import type { MicroflowRunSession } from "@atlas/microflow";

/**
 * 运行期持久化以 `MicroflowRunSession` 为逻辑契约；`trace`/`logs` 可拆表（见 `storage-types`）。
 * RunSession **不**写回 AuthoringSchema。
 */
export type { MicroflowRunSession };

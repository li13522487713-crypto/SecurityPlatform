import { useMemo, useState } from "react";

import type { MicroflowAuthoringSchema } from "../schema/types";
import { MicroflowHistoryManager, type MicroflowHistoryManagerOptions } from "./microflow-history-manager";
import type { MicroflowHistoryState } from "./history-types";

export function useMicroflowHistory(
  initialSchema: MicroflowAuthoringSchema,
  options?: MicroflowHistoryManagerOptions,
): [MicroflowHistoryManager, MicroflowHistoryState, () => void] {
  const manager = useMemo(() => {
    const instance = new MicroflowHistoryManager(options);
    instance.init(initialSchema);
    return instance;
  }, []);
  const [state, setState] = useState<MicroflowHistoryState>(() => manager.getState());
  const refresh = () => setState(manager.getState());
  return [manager, state, refresh];
}

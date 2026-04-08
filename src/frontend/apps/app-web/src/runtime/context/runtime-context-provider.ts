import type { InjectionKey } from "vue";
import { inject, provide, readonly, toRefs } from "vue";
import { storeToRefs } from "pinia";
import { useRuntimeContextStore } from "./runtime-context-store";
import type { RuntimeContext } from "./runtime-context-types";

const RUNTIME_CONTEXT_KEY: InjectionKey<ReturnType<typeof createRuntimeContextProvision>> =
  Symbol("RuntimeContext");

export function createRuntimeContextProvision() {
  const store = useRuntimeContextStore();
  const { context, initialized } = storeToRefs(store);

  return {
    context: readonly(context),
    initialized: readonly(initialized),
    setRecord: store.setRecord.bind(store),
    setSelection: store.setSelection.bind(store),
    setGlobalVar: store.setGlobalVar.bind(store),
    setPageMode: store.setPageMode.bind(store),
    getExpressionVariables: store.getExpressionVariables.bind(store),
  };
}

export type RuntimeContextProvision = ReturnType<typeof createRuntimeContextProvision>;

/**
 * 在 RuntimePageHost 中调用以向子组件提供运行上下文。
 */
export function provideRuntimeContext(): RuntimeContextProvision {
  const provision = createRuntimeContextProvision();
  provide(RUNTIME_CONTEXT_KEY, provision);
  return provision;
}

/**
 * 子组件中注入运行上下文。
 */
export function useRuntimeContext(): RuntimeContextProvision {
  const injected = inject(RUNTIME_CONTEXT_KEY);
  if (!injected) {
    throw new Error(
      "[RuntimeContext] useRuntimeContext() must be called within a RuntimePageHost tree.",
    );
  }
  return injected;
}

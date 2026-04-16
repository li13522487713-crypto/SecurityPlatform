import { lazy, type ComponentType, type LazyExoticComponent } from "react";

type ExtractComponent<TValue> = TValue extends ComponentType<infer TProps>
  ? ComponentType<TProps>
  : never;

export function lazyNamed<TModule, TKey extends keyof TModule>(
  loader: () => Promise<TModule>,
  key: TKey
): LazyExoticComponent<ExtractComponent<TModule[TKey]>> {
  return lazy(async () => {
    const module = await loader();
    return {
      default: module[key] as ExtractComponent<TModule[TKey]>
    };
  });
}

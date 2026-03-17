declare module "lodash-es" {
  export function debounce<T extends (...args: never[]) => unknown>(
    func: T,
    wait?: number
  ): ((...args: Parameters<T>) => void) & {
    cancel(): void;
    flush(): ReturnType<T> | undefined;
  };
}

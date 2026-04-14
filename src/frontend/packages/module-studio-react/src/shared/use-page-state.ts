import { useState, useCallback, useEffect, useRef } from "react";

export type PageStatus = "idle" | "loading" | "empty" | "error" | "ready";

export interface PageState<T> {
  status: PageStatus;
  data: T | null;
  error: Error | null;
  reload: () => Promise<void>;
}

export function usePageState<T>(
  loader: () => Promise<T>,
  options?: {
    immediate?: boolean;
    isEmpty?: (data: T) => boolean;
  }
): PageState<T> {
  const [status, setStatus] = useState<PageStatus>("idle");
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<Error | null>(null);
  
  const loaderRef = useRef(loader);
  loaderRef.current = loader;
  
  const reload = useCallback(async () => {
    setStatus("loading");
    setError(null);
    try {
      const result = await loaderRef.current();
      setData(result);
      
      const isEmpty = options?.isEmpty ? options.isEmpty(result) : 
        (Array.isArray(result) && result.length === 0) || result === null || result === undefined;
        
      if (isEmpty) {
        setStatus("empty");
      } else {
        setStatus("ready");
      }
    } catch (e) {
      setError(e instanceof Error ? e : new Error(String(e)));
      setStatus("error");
    }
  }, [options?.isEmpty]);

  useEffect(() => {
    if (options?.immediate !== false) {
      void reload();
    }
  }, [reload, options?.immediate]);

  return { status, data, error, reload };
}

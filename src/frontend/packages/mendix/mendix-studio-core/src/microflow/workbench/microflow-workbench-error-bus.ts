import type { MicroflowApiError } from "../contracts/api/api-envelope";

export interface MicroflowWorkbenchErrorBusSnapshot {
  activeError?: MicroflowApiError;
  readonlyReason?: MicroflowApiError;
}

export interface MicroflowWorkbenchErrorBusOptions {
  onUnauthorizedRedirect: () => void;
  onOpenProblems: () => void;
}

type Listener = (snapshot: MicroflowWorkbenchErrorBusSnapshot) => void;

export class MicroflowWorkbenchErrorBus {
  private snapshot: MicroflowWorkbenchErrorBusSnapshot = {};
  private readonly listeners = new Set<Listener>();
  private cleanup?: () => void;

  constructor(private readonly options: MicroflowWorkbenchErrorBusOptions) {}

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    listener(this.snapshot);
    return () => {
      this.listeners.delete(listener);
    };
  }

  getSnapshot(): MicroflowWorkbenchErrorBusSnapshot {
    return this.snapshot;
  }

  attach(): () => void {
    if (typeof window === "undefined") {
      return () => undefined;
    }

    const handleUnauthorized = () => {
      this.options.onUnauthorizedRedirect();
    };

    const handleForbidden = () => {
      this.setSnapshot({
        ...this.snapshot,
        activeError: {
          code: "MICROFLOW_PERMISSION_DENIED",
          category: "permission",
          message: "当前账号无权限执行该微流工作台操作。",
          httpStatus: 403,
        },
        readonlyReason: {
          code: "MICROFLOW_PERMISSION_DENIED",
          category: "permission",
          message: "当前账号无权限执行该微流工作台操作。",
          httpStatus: 403,
        },
      });
    };

    const handleApiError = (event: Event) => {
      const detail = (event as CustomEvent<MicroflowApiError>).detail;
      if (!detail || detail.category === "conflict") {
        return;
      }
      if (detail.category === "auth") {
        this.options.onUnauthorizedRedirect();
        return;
      }
      if (detail.category === "validation" && detail.validationIssues?.length) {
        this.options.onOpenProblems();
      }
      this.setSnapshot({
        ...this.snapshot,
        activeError: detail,
        readonlyReason: detail.category === "permission" ? detail : this.snapshot.readonlyReason,
      });
    };

    window.addEventListener("atlas:microflow-unauthorized", handleUnauthorized);
    window.addEventListener("atlas:microflow-forbidden", handleForbidden);
    window.addEventListener("atlas:microflow-api-error", handleApiError as EventListener);

    this.cleanup = () => {
      window.removeEventListener("atlas:microflow-unauthorized", handleUnauthorized);
      window.removeEventListener("atlas:microflow-forbidden", handleForbidden);
      window.removeEventListener("atlas:microflow-api-error", handleApiError as EventListener);
    };
    return this.cleanup;
  }

  clearActiveError(): void {
    this.setSnapshot({
      ...this.snapshot,
      activeError: undefined,
    });
  }

  clearReadonlyReason(): void {
    this.setSnapshot({
      ...this.snapshot,
      readonlyReason: undefined,
    });
  }

  dispose(): void {
    this.cleanup?.();
  }

  private setSnapshot(snapshot: MicroflowWorkbenchErrorBusSnapshot): void {
    this.snapshot = snapshot;
    for (const listener of this.listeners) {
      listener(this.snapshot);
    }
  }
}

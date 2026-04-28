import { syncKbCounters } from "./fixtures";
import type { MockStore } from "./store";
import type { KnowledgeJob, KnowledgeJobStatus } from "../types";

type Listener = (job: KnowledgeJob) => void;

interface JobMachineEntry {
  jobId: number;
  /** 状态机阶段 0..3 对应：Queued → Running → progress 推进 → Succeeded */
  phase: number;
}

/**
 * Mock scheduler：周期性把 Queued/Running 任务推进到 Succeeded（或 Failed），
 * 并广播给订阅方（详情页 / 任务面板）。设计为单进程、无线程、纯 JS timer，
 * 关闭定时器后状态保持不变，方便单测断言或在测试里用 advanceUntilStable 同步走完。
 */
export class JobScheduler {
  private timer: ReturnType<typeof setInterval> | null = null;
  private listeners = new Set<Listener>();
  private kbListeners = new Map<number, Set<Listener>>();
  private machine = new Map<number, JobMachineEntry>();
  private intervalMs: number;

  constructor(
    private readonly store: MockStore,
    intervalMs: number
  ) {
    this.intervalMs = intervalMs;
  }

  start(): void {
    if (this.timer || this.intervalMs <= 0) {
      return;
    }
    this.timer = setInterval(() => this.tick(), this.intervalMs);
  }

  stop(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  enqueue(job: KnowledgeJob, options?: { simulateFailure?: boolean }): void {
    this.store.state.jobs.set(job.id, job);
    this.machine.set(job.id, { jobId: job.id, phase: 0 });
    if (options?.simulateFailure) {
      // 标记失败：scheduler 推进时检测 errorMessage 标签
      job.errorMessage = "MOCK_SIMULATED_FAILURE";
    }
    this.notify(job);
    this.start();
  }

  retry(jobId: number): void {
    const job = this.store.state.jobs.get(jobId);
    if (!job) return;
    job.status = "Retrying";
    job.attempts = Math.min(job.attempts + 1, job.maxAttempts);
    job.progress = 10;
    job.errorMessage = undefined;
    job.logs.push({ ts: new Date().toISOString(), level: "info", message: `Retry attempt ${job.attempts}` });
    this.machine.set(jobId, { jobId, phase: 1 });
    this.notify(job);
    this.start();
  }

  cancel(jobId: number): void {
    const job = this.store.state.jobs.get(jobId);
    if (!job) return;
    job.status = "Canceled";
    job.finishedAt = new Date().toISOString();
    job.logs.push({ ts: job.finishedAt, level: "warn", message: "Job canceled by user" });
    this.machine.delete(jobId);
    syncKbCounters(this.store, job.knowledgeBaseId);
    this.notify(job);
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  subscribeKb(knowledgeBaseId: number, listener: Listener): () => void {
    let bucket = this.kbListeners.get(knowledgeBaseId);
    if (!bucket) {
      bucket = new Set();
      this.kbListeners.set(knowledgeBaseId, bucket);
    }
    bucket.add(listener);
    return () => {
      const current = this.kbListeners.get(knowledgeBaseId);
      if (!current) return;
      current.delete(listener);
      if (current.size === 0) {
        this.kbListeners.delete(knowledgeBaseId);
      }
    };
  }

  /**
   * 同步推进所有任务到终态。仅供单测使用。
   * @param maxIterations 防御性上限，避免错误导致死循环
   */
  advanceUntilStable(maxIterations = 64): void {
    let i = 0;
    while (this.machine.size > 0 && i < maxIterations) {
      this.tick();
      i += 1;
    }
  }

  private tick(): void {
    if (this.machine.size === 0) {
      this.stop();
      return;
    }

    const finishedKbIds = new Set<number>();
    this.machine.forEach((entry, jobId) => {
      const job = this.store.state.jobs.get(jobId);
      if (!job) {
        this.machine.delete(jobId);
        return;
      }

      const nextStatus = nextStatusFor(job, entry.phase);
      job.status = nextStatus.status;
      job.progress = nextStatus.progress;

      if (nextStatus.status === "Running" && !job.startedAt) {
        job.startedAt = new Date().toISOString();
      }
      if (nextStatus.log) {
        job.logs.push(nextStatus.log);
      }
      if (nextStatus.terminal) {
        job.finishedAt = new Date().toISOString();
        if (nextStatus.status === "Failed" || nextStatus.status === "DeadLetter") {
          job.errorMessage = job.errorMessage === "MOCK_SIMULATED_FAILURE"
            ? "Mock simulated parsing failure"
            : job.errorMessage;
        }
        this.machine.delete(jobId);
        finishedKbIds.add(job.knowledgeBaseId);
      } else {
        entry.phase += 1;
      }
      this.notify(job);
    });

    finishedKbIds.forEach(kbId => syncKbCounters(this.store, kbId));

    if (this.machine.size === 0) {
      this.stop();
    }
  }

  private notify(job: KnowledgeJob): void {
    this.listeners.forEach(listener => {
      try {
        listener(job);
      } catch {
        // 忽略订阅方异常，避免影响其它监听者
      }
    });
    const bucket = this.kbListeners.get(job.knowledgeBaseId);
    if (bucket) {
      bucket.forEach(listener => {
        try {
          listener(job);
        } catch {
          // 同上
        }
      });
    }
  }
}

interface NextStatus {
  status: KnowledgeJobStatus;
  progress: number;
  terminal: boolean;
  log?: { ts: string; level: "info" | "warn" | "error"; message: string };
}

function nextStatusFor(job: KnowledgeJob, phase: number): NextStatus {
  const ts = new Date().toISOString();
  const willFail = job.errorMessage === "MOCK_SIMULATED_FAILURE";

  if (phase === 0) {
    return {
      status: "Running",
      progress: 25,
      terminal: false,
      log: { ts, level: "info", message: `${job.type} job started` }
    };
  }

  if (phase === 1) {
    return {
      status: "Running",
      progress: 60,
      terminal: false,
      log: { ts, level: "info", message: `${job.type} job in progress` }
    };
  }

  if (phase === 2) {
    return {
      status: "Running",
      progress: 90,
      terminal: false,
      log: { ts, level: "info", message: `${job.type} job finalizing` }
    };
  }

  if (willFail) {
    if (job.attempts < job.maxAttempts) {
      return {
        status: "Retrying",
        progress: 10,
        terminal: true,
        log: { ts, level: "warn", message: `${job.type} job will retry (#${job.attempts + 1})` }
      };
    }
    return {
      status: "DeadLetter",
      progress: 100,
      terminal: true,
      log: { ts, level: "error", message: `${job.type} job moved to dead-letter` }
    };
  }

  return {
    status: "Succeeded",
    progress: 100,
    terminal: true,
    log: { ts, level: "info", message: `${job.type} job completed` }
  };
}

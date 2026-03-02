import { onMounted } from "vue";
import { driver, type DriveStep, type Config } from "driver.js";
import "driver.js/dist/driver.css";

export interface OnboardingOptions {
  /** 本地存储的键名，用于记录是否已查看引导 */
  storageKey: string;
  /** 引导步骤 */
  steps: DriveStep[];
  /** 是否在首次访问时自动显示（默认true） */
  showOnFirstVisit?: boolean;
  /** 引导完成回调 */
  onComplete?: () => void;
  /** 引导销毁回调 */
  onDestroy?: () => void;
}

/**
 * 新手引导Composable
 *
 * 使用driver.js实现产品引导功能
 *
 * @example
 * ```ts
 * const { startTour, resetTour } = useOnboarding({
 *   storageKey: 'form-designer-tour',
 *   steps: [
 *     {
 *       element: '.toolbar-left',
 *       popover: {
 *         title: '欢迎',
 *         description: '欢迎使用表单设计器'
 *       }
 *     }
 *   ]
 * });
 * ```
 */
export function useOnboarding(options: OnboardingOptions) {
  const {
    storageKey,
    steps,
    showOnFirstVisit = true,
    onComplete,
    onDestroy
  } = options;

  /** 检查用户是否已查看过引导 */
  const hasViewedTour = (): boolean => {
    return localStorage.getItem(storageKey) === "true";
  };

  /** 标记用户已查看引导 */
  const markTourAsViewed = (): void => {
    localStorage.setItem(storageKey, "true");
  };

  /** 重置引导状态（用于"再次查看引导"功能） */
  const resetTour = (): void => {
    localStorage.removeItem(storageKey);
  };

  /** 启动引导 */
  const startTour = (): void => {
    const driverConfig: Config = {
      showProgress: true,
      showButtons: ["next", "previous"],
      steps,
      onDestroyStarted: () => {
        markTourAsViewed();
        onDestroy?.();
      },
      onDestroyed: () => {
        onComplete?.();
      },
      nextBtnText: "下一步",
      prevBtnText: "上一步",
      doneBtnText: "完成",
      progressText: "{{current}} / {{total}}"
    };

    const driverObj = driver(driverConfig);
    driverObj.drive();
  };

  /** 在组件挂载时自动显示引导（首次访问） */
  if (showOnFirstVisit) {
    onMounted(() => {
      setTimeout(() => {
        if (!hasViewedTour()) {
          startTour();
        }
      }, 800); // 延迟800ms，确保页面元素已渲染
    });
  }

  return {
    startTour,
    resetTour,
    hasViewedTour
  };
}

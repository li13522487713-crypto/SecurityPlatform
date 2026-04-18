import { vi } from 'vitest';

/**
 * Semi UI 的部分组件（Empty / Spin 等）依赖 lottie-web 渲染加载/空态动画。
 * lottie-web 在模块加载阶段就会 new HTMLCanvasElement().getContext('2d')，
 * 而 jsdom 默认 getContext 返回 null 导致整个 suite import 阶段抛错。
 * 这里用一份最小 fake CanvasRenderingContext2D polyfill，让 lottie-web 加载阶段不炸。
 * 真正绘制不会发生（jsdom 没有渲染管线），单测也不需要看动画。
 */
function createFakeCanvasContext(): unknown {
  const noop = () => {};
  return new Proxy(
    {
      canvas: {} as HTMLCanvasElement,
      fillStyle: '',
      strokeStyle: '',
      lineWidth: 1,
      globalAlpha: 1,
      globalCompositeOperation: 'source-over',
      font: '10px sans-serif',
      textAlign: 'start',
      textBaseline: 'alphabetic',
      filter: 'none',
      lineCap: 'butt',
      lineJoin: 'miter',
      miterLimit: 10,
      shadowBlur: 0,
      shadowColor: 'rgba(0, 0, 0, 0)',
      shadowOffsetX: 0,
      shadowOffsetY: 0,
    },
    {
      get(target, prop: string) {
        if (prop in target) {
          return (target as Record<string, unknown>)[prop];
        }
        if (prop === 'getImageData') {
          return () => ({ data: new Uint8ClampedArray([0, 0, 0, 0]), width: 1, height: 1 });
        }
        if (prop === 'createImageData') {
          return () => ({ data: new Uint8ClampedArray([0, 0, 0, 0]), width: 1, height: 1 });
        }
        if (prop === 'measureText') {
          return () => ({ width: 0 });
        }
        return noop;
      },
      set(target, prop: string, value: unknown) {
        (target as Record<string, unknown>)[prop] = value;
        return true;
      },
    },
  );
}

if (typeof HTMLCanvasElement !== 'undefined') {
  HTMLCanvasElement.prototype.getContext = function getContextStub() {
    return createFakeCanvasContext() as CanvasRenderingContext2D;
  } as unknown as HTMLCanvasElement['getContext'];
}

/**
 * Semi 的部分组件还会调用 Element.prototype.scrollIntoView，jsdom 也未实现。
 */
if (typeof Element !== 'undefined' && !Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = vi.fn();
}

/**
 * Semi Modal / SideSheet 通过 ResizeObserver 监听容器尺寸变化，jsdom 同样未实现。
 */
class MockResizeObserver {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}
if (typeof globalThis.ResizeObserver === 'undefined') {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (globalThis as any).ResizeObserver = MockResizeObserver;
}

if (typeof window !== 'undefined' && typeof window.matchMedia === 'undefined') {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
}

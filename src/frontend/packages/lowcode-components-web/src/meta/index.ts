export * from './categories';

import { ALL_METAS } from './categories';
import { registerComponent, type ComponentImplementationDescriptor } from '@atlas/lowcode-component-registry';

/**
 * 把 47 个内置组件全量注册到 component-registry。
 * 每个组件以 thin Semi 包装实现（实际 React 组件由 M07 lowcode-studio-web 渲染层装配；本包仅承载 ComponentMeta）。
 *
 * 元数据驱动校验：所有组件都声明 implementationDescriptor，仅引用 React + Semi。
 */
export function registerAllWebComponents(): void {
  const descriptor: ComponentImplementationDescriptor = {
    importedGlobals: [],
    importedPackages: ['react', '@douyinfe/semi-ui', '@douyinfe/semi-icons']
  };
  for (const meta of ALL_METAS) {
    registerComponent(meta, { implementationDescriptor: descriptor });
  }
}

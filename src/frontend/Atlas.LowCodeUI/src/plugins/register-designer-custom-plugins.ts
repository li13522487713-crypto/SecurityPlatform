/**
 * 设计器挂载前一次性注册自定义组件到 amis-editor 工具箱（与渲染器注册配合使用）
 */
import type { CustomReactComponentDef, CustomSdkComponentDef } from "@/types/amis";
import { registerSdkEditorPlugin } from "./register-custom-sdk";
import { registerReactComponents } from "./register-react-component";

const registeredSdkTypes = new Set<string>();
const registeredReactTypes = new Set<string>();

export interface RegisterDesignerCustomPluginsOptions {
  sdkComponents?: CustomSdkComponentDef[];
  reactComponents?: CustomReactComponentDef[];
}

/**
 * 在挂载 amis-editor Editor 之前调用：注册 SDK custom 与 React 自定义渲染器到工具箱。
 * 同一 type 仅注册一次，避免重复 registerEditorPlugin。
 */
export async function registerDesignerCustomPluginsOnce(options: RegisterDesignerCustomPluginsOptions): Promise<void> {
  const sdk = options.sdkComponents ?? [];
  for (const def of sdk) {
    if (registeredSdkTypes.has(def.type)) {
      continue;
    }
    await registerSdkEditorPlugin(def);
    registeredSdkTypes.add(def.type);
  }

  const react = options.reactComponents ?? [];
  const toRegisterReact = react.filter((d) => !registeredReactTypes.has(d.type));
  for (const def of toRegisterReact) {
    registeredReactTypes.add(def.type);
  }
  if (toRegisterReact.length > 0) {
    await registerReactComponents(toRegisterReact);
  }
}

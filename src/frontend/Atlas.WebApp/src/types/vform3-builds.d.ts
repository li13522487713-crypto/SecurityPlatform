declare module 'vform3-builds' {
  import type { Plugin, DefineComponent } from 'vue';
  export const VFormDesigner: DefineComponent;
  export const VFormRender: DefineComponent;
  const plugin: Plugin & {
    VFormDesigner: DefineComponent;
    VFormRender: DefineComponent;
  };
  export default plugin;
}

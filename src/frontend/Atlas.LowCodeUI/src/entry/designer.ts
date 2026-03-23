/**
 * 设计器入口 — 含 AmisDesigner（默认挂载 amis-editor 纯拖拽画布，见 autoLoadEditor）
 */
import "../styles/amis-theme.css";
import "../styles/designer.css";

export type { AmisDesignerProps, AmisDesignerEmits } from "../types/amis";

export { default as AmisDesigner } from "../components/AmisDesigner/index.vue";

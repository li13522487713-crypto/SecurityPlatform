import { use } from "echarts/core";
import { LineChart } from "echarts/charts";
import {
  DatasetComponent,
  GridComponent,
  LegendComponent,
  TitleComponent,
  TooltipComponent,
  TransformComponent
} from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";

// ECharts 需要显式注册 renderer/chart/component，避免运行时 renderer 未定义。
use([
  LineChart,
  GridComponent,
  TooltipComponent,
  TitleComponent,
  LegendComponent,
  DatasetComponent,
  TransformComponent,
  CanvasRenderer
]);

import { Tag } from "@douyinfe/semi-ui";

import type { MicroflowImpactLevel } from "./microflow-reference-types";
import { getImpactLevelColor, getImpactLevelLabel } from "./microflow-reference-utils";

export function MicroflowReferenceImpactTag({ level }: { level: MicroflowImpactLevel }) {
  return <Tag color={getImpactLevelColor(level)}>{getImpactLevelLabel(level)}</Tag>;
}

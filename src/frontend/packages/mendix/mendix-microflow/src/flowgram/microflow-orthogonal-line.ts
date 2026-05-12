import { WorkflowFoldLineContribution } from "@flowgram-adapter/free-layout-editor";

export class MicroflowOrthogonalLineContribution extends WorkflowFoldLineContribution {
  // The parent declares type as LineType (numeric enum). We use a string key so
  // WorkflowLinesManager registers this as a distinct contribution. The double cast
  // satisfies TypeScript's override-compatibility check while keeping the string value.
  static override type = "microflow-orthogonal" as unknown as typeof WorkflowFoldLineContribution.type;

  override get path(): string {
    // getBend() produces: "L a,bQ c,d e,f"  where Q control point (c,d) is the corner vertex
    // Replace with:       "L c,d"           to get a strict 90° turn without Bezier curves
    return super.path.replace(
      /L\s*([-\d.]+),([-\d.]+)Q\s*([-\d.]+),([-\d.]+)\s+([-\d.]+),([-\d.]+)/g,
      "L $3,$4",
    );
  }
}

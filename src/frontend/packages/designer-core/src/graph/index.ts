export interface DesignerNode {
  id: string;
  type: string;
  data: Record<string, unknown>;
}

export interface DesignerEdge {
  id: string;
  source: string;
  target: string;
}

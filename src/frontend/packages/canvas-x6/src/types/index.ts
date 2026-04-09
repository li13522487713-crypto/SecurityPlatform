export interface CanvasNode {
  id: string
  shape: string
  label?: string
  x: number
  y: number
  width?: number
  height?: number
  data?: Record<string, unknown>
}

export interface CanvasEdge {
  id: string
  source: string
  target: string
  label?: string
  data?: Record<string, unknown>
}

export interface CanvasGraphDocument {
  nodes: CanvasNode[]
  edges: CanvasEdge[]
  metadata?: Record<string, unknown>
}

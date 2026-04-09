export * from './types'

export function createCanvasDocument(): import('./types').CanvasGraphDocument {
  return {
    nodes: [],
    edges: []
  }
}

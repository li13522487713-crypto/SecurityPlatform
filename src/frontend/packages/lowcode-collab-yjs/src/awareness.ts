/**
 * Awareness：多人光标 / 选区 / 选中组件高亮（M16 C16-2）。
 */

import * as Y from 'yjs';
import { Awareness } from 'y-protocols/awareness';

export interface AwarenessUserState {
  userId: string;
  username: string;
  /** 当前选中的组件 id（undefined 表示未选中）。*/
  selectedComponentId?: string;
  /** 鼠标光标坐标（画布相对）。*/
  cursor?: { x: number; y: number };
  /** 颜色（用于头像 / 高亮框边框）。*/
  color?: string;
}

export class CollabAwareness {
  public readonly awareness: Awareness;

  constructor(public readonly doc: Y.Doc) {
    this.awareness = new Awareness(doc);
  }

  setLocal(state: AwarenessUserState): void {
    this.awareness.setLocalState(state);
  }

  getRemoteStates(): Map<number, AwarenessUserState> {
    const out = new Map<number, AwarenessUserState>();
    for (const [clientId, state] of this.awareness.getStates().entries()) {
      if (clientId === this.doc.clientID) continue;
      out.set(clientId, state as AwarenessUserState);
    }
    return out;
  }

  destroy(): void {
    this.awareness.destroy();
  }
}

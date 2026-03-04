import type {
  ApprovalFlowTree,
  TreeNode,
  StartNode,
  ApproveNode,
  CopyNode,
  ConditionNode,
  EndNode,
  ConditionBranch,
  DynamicConditionNode,
  ParallelConditionNode,
  ParallelNode,
  InclusiveNode,
  RouteNode,
  CallProcessNode,
  TimerNode,
  TriggerNode,
  NodeType
} from '@/types/approval-tree';
import type {
  ApprovalDefinitionJson,
  ApprovalDefinitionMeta,
  LfFormPayload,
  AmisFormPayload,
  ApprovalNode,
  ConditionBranch as DefinitionBranch,
  JsonValue
} from '@/types/approval-definition';
import type { Node, Edge } from '@antv/x6';
import { nanoid } from 'nanoid';

export class ApprovalTreeConverter {
  /**
   * 树结构 → X6 图结构（用于渲染或保存）
   */
  static treeToGraph(tree: ApprovalFlowTree): { nodes: Node.Metadata[], edges: Edge.Metadata[] } {
    const nodes: Node.Metadata[] = [];
    const edges: Edge.Metadata[] = [];
    
    // 递归遍历树节点，生成 X6 节点和边
    this.traverseTree(tree.rootNode, null, nodes, edges, { x: 100, y: 100 });
    
    return { nodes, edges };
  }
  
  /**
   * X6 图结构 → 树结构（从图反推树，用于编辑现有流程）
   */
  static graphToTree(nodes: GraphNode[], edges: GraphEdge[]): ApprovalFlowTree {
    if (!nodes || nodes.length === 0) {
      // 返回默认空树
      return this.createDefaultTree();
    }

    // 找到开始节点
    const startNodeData = nodes.find(n => n.data?.type === 'start' || n.type === 'start');
    if (!startNodeData) {
       console.warn('缺少开始节点, 返回默认树');
       return this.createDefaultTree();
    }
    
    // 构建邻接表方便查找
    const outgoing = new Map<string, GraphEdge[]>();
    edges.forEach(edge => {
      const source = getTerminalCellId(edge.source);
      if (!source) return;
      if (!outgoing.has(source)) outgoing.set(source, []);
      outgoing.get(source)?.push(edge);
    });

    // 递归构建树
    const startNodeId = startNodeData.id;
    if (!startNodeId) {
      console.warn('开始节点缺少ID, 返回默认树');
      return this.createDefaultTree();
    }
    const rootNode = this.buildTreeFromNode(startNodeId, nodes, outgoing);
    if (rootNode.nodeType !== 'start') {
      console.warn('开始节点类型异常, 返回默认树');
      return this.createDefaultTree();
    }

    return {
      flowName: '',
      rootNode
    };
  }

  static createDefaultTree(): ApprovalFlowTree {
    return {
      flowName: '',
      rootNode: {
        id: nanoid(),
        nodeType: 'start',
        nodeName: '发起人',
        childNode: {
          id: nanoid(),
          nodeType: 'end',
          nodeName: '结束'
        }
      }
    };
  }
  
  /**
   * 树结构 → 后端 definitionJson（nodes + edges 格式）
   */
  static treeToDefinitionJson(
    tree: ApprovalFlowTree,
    meta?: ApprovalDefinitionMeta,
    lfForm?: LfFormPayload,
    amisForm?: AmisFormPayload
  ): string {
    const definition: ApprovalDefinitionJson = {
      meta: {
        flowName: meta?.flowName ?? tree.flowName,
        description: meta?.description,
        category: meta?.category,
        // visibilityScope 不写入 definitionJson.meta，统一通过顶层 visibilityScopeJson 字段管理，避免双写不一致
        isQuickEntry: meta?.isQuickEntry,
        isLowCodeFlow: meta?.isLowCodeFlow
      },
      lfForm,
      amisForm,
      nodes: {
        rootNode: this.treeNodeToDefinition(tree.rootNode)
      }
    };
    return JSON.stringify(definition);
  }
  
  /**
   * 后端 definitionJson → 树结构
   */
  static definitionJsonToState(json: string): {
    tree: ApprovalFlowTree;
    meta?: ApprovalDefinitionMeta;
    lfForm?: LfFormPayload;
    amisForm?: AmisFormPayload;
  } {
    if (!json || json === '{}') {
      return { tree: this.createDefaultTree() };
    }

    try {
      const parsed = JSON.parse(json);
      if (parsed?.nodes?.rootNode) {
        const rootNode = this.definitionNodeToTree(parsed.nodes.rootNode);
        if (rootNode.nodeType !== 'start') {
          return { tree: this.createDefaultTree(), meta: parsed.meta, lfForm: parsed.lfForm, amisForm: parsed.amisForm };
        }
        const tree: ApprovalFlowTree = {
          flowName: parsed.meta?.flowName ?? '',
          rootNode
        };
        return {
          tree,
          meta: parsed.meta,
          lfForm: parsed.lfForm,
          amisForm: parsed.amisForm
        };
      }

      if (parsed?.nodes && parsed?.edges) {
        const tree = this.graphToTree(parsed.nodes || [], parsed.edges || []);
        return { tree };
      }
    } catch (e) {
      console.error('Failed to parse definition json', e);
    }

    return { tree: this.createDefaultTree() };
  }

  static definitionJsonToTree(json: string): ApprovalFlowTree {
    return this.definitionJsonToState(json).tree;
  }
  
  // 私有辅助方法：遍历树生成图
  private static traverseTree(
    node: TreeNode | undefined,
    parentId: string | null,
    nodes: GraphNode[],
    edges: GraphEdge[],
    position: { x: number, y: number }
  ): string | null {
    if (!node) return null;

    // 添加节点
    const nodeData: GraphNodeData = {
        id: node.id,
        type: node.nodeType,
        label: node.nodeName,
        x: position.x,
        y: position.y,
        // 特定属性
        assigneeType: (node as ApproveNode).assigneeType,
        assigneeValue: (node as ApproveNode).assigneeValue,
        approvalMode: (node as ApproveNode).approvalMode,
        copyToUsers: (node as CopyNode).copyToUsers,
        // conditionRule 存储在边上，或者如果是条件节点，可能需要特殊处理
        // 在 AntFlow 原有逻辑中，条件节点本身不存 rule，rule 存在从条件节点出去的 edge 上
    };
    
    // 规范化数据，确保 X6 可以读取
    nodes.push({
      id: node.id,
      shape: 'custom-node', // 假设使用自定义节点 shape
      x: position.x,
      y: position.y,
      label: node.nodeName,
      data: nodeData,
      type: node.nodeType // 兼容旧逻辑，直接放在顶层属性
    });

    // 如果有父节点，建立连线
    if (parentId) {
       // 注意：如果是条件分支，连线可能需要携带条件规则
       // 这里 traverseTree 主要是处理主干。条件分支在下面单独处理。
       // 这里的 parentId 是指直接的前驱节点
       edges.push({
         source: parentId,
         target: node.id,
         data: {} 
       });
    }

    let lastNodeId = node.id;
    let nextY = position.y + 100;

    // 处理条件/动态条件/条件并行/包容分支节点
    if (node.nodeType === 'condition' || node.nodeType === 'dynamicCondition' || node.nodeType === 'parallelCondition' || node.nodeType === 'inclusive') {
      const conditionNode = node as ConditionNode | DynamicConditionNode | ParallelConditionNode | InclusiveNode;
      // 条件节点本身是一个分流点。
      // 它的 conditionNodes (branches) 是并行的。
      // 每个 branch 的第一个节点连接自 conditionNode。
      
      // 收集所有分支的汇聚点（如果有）
      // 在树形结构中，分支最终会汇聚到 conditionNode.childNode (如果有)
      // 或者分支各自结束。
      
      // 我们需要递归处理每个分支
      const branchCount = conditionNode.conditionNodes.length;
      let startX = position.x - (branchCount - 1) * 100; // 简单排版

      const branchEndIds: string[] = [];

      conditionNode.conditionNodes.forEach((branch, index) => {
         // 分支的第一个节点（如果有）
         // 实际上 branch.childNode 是分支的开始节点
         if (branch.childNode) {
            // 建立从 conditionNode 到 branch.childNode 的连线
            // 这条连线需要携带条件
            const branchFirstNodeId = branch.childNode.id;
            
            // 递归处理分支子树
            // 分支的 parentId 是 conditionNode.id
            // 但是我们需要在 edge 上加条件，所以不能直接用 traverseTree 的默认连线逻辑
            // 我们手动调用 traverseTree 但不传 parentId，然后手动加 edge
            
            const branchLastId = this.traverseTree(branch.childNode, null, nodes, edges, { x: startX + index * 200, y: nextY });
            
            if (branchLastId) {
                branchEndIds.push(branchLastId);
            }

            // 添加带条件的连线
            const branchEdgeData: GraphEdgeData = {};
            if (branch.conditionRule) {
                branchEdgeData.conditionRule = branch.conditionRule;
            }
            edges.push({
                source: conditionNode.id,
                target: branchFirstNodeId,
                data: branchEdgeData,
                label: branch.branchName // 可选：在线上显示分支名
            });
         } else {
             // 空分支？直接连接到汇聚点？
             // 如果分支为空，意味着直接通过。
             branchEndIds.push(conditionNode.id); 
             // 这种情况下，如果 conditionNode 后面有 childNode，
             // 那么应该有一条线从 conditionNode 直接连到 childNode，且带有该分支的条件。
             // 但这样会导致多条线连到同一个 childNode。
             // 更好的做法是：空分支不生成中间节点，直接连到汇聚点。
             // 待会儿处理汇聚时，如果 branchEndId 是 conditionNode.id，说明是空分支。
         }
      });

      // 处理汇聚
      if (conditionNode.childNode) {
          // 递归生成汇聚后的节点
          // 它的位置在所有分支下面
          const mergeY = nextY + 200; // 简化计算
          const childLastId = this.traverseTree(conditionNode.childNode, null, nodes, edges, { x: position.x, y: mergeY });
          
          // 将所有分支的末端连接到 childNode
          const mergeTargetId = conditionNode.childNode.id;
          
          branchEndIds.forEach((endId, index) => {
              const branch = conditionNode.conditionNodes[index];
              const mergeEdgeData: GraphEdgeData = {};
              if (branch.conditionRule) {
                  mergeEdgeData.conditionRule = branch.conditionRule;
              }
              
              if (endId === conditionNode.id) {
                  // 空分支，直接从 conditionNode 连到 mergeTargetId
                  edges.push({
                      source: conditionNode.id,
                      target: mergeTargetId,
                      data: mergeEdgeData,
                      label: branch.branchName
                  });
              } else {
                  // 分支末端连到 mergeTargetId
                  edges.push({
                      source: endId,
                      target: mergeTargetId,
                      data: {}
                  });
              }
          });
          
          if (childLastId) lastNodeId = childLastId;
      } else {
          // 没有汇聚节点，分支各自结束
          // 如果分支末端不是 EndNode，可能需要补 EndNode？
          // 在树结构定义中，EndNode 是明确的节点。
          // 如果分支里有 EndNode，traverseTree 会处理。
      }

      return lastNodeId;
    }

    // 处理并行审批节点
    if (node.nodeType === 'parallel') {
      const parallelNode = node as ParallelNode;
      const branchCount = parallelNode.parallelNodes.length;
      let startX = position.x - (branchCount - 1) * 100;

      const branchEndIds: string[] = [];
      parallelNode.parallelNodes.forEach((branch, index) => {
        const branchLastId = this.traverseTree(branch, null, nodes, edges, { x: startX + index * 200, y: nextY });
        if (branchLastId) {
          branchEndIds.push(branchLastId);
        }
        edges.push({
          source: parallelNode.id,
          target: branch.id,
          data: {},
          label: branch.nodeName
        });
      });

      if (parallelNode.childNode) {
        const mergeY = nextY + 200;
        const childLastId = this.traverseTree(parallelNode.childNode, null, nodes, edges, { x: position.x, y: mergeY });
        const mergeTargetId = parallelNode.childNode.id;
        branchEndIds.forEach((endId) => {
          edges.push({
            source: endId,
            target: mergeTargetId,
            data: {}
          });
        });
        if (childLastId) lastNodeId = childLastId;
      }

      return lastNodeId;
    }

    // 处理普通节点的后续
    if ('childNode' in node && node.childNode) {
        return this.traverseTree(node.childNode, node.id, nodes, edges, { x: position.x, y: nextY });
    }

    return lastNodeId;
  }

  private static treeNodeToDefinition(node: TreeNode): ApprovalNode {
    const childNode = 'childNode' in node ? node.childNode : undefined;
    const base: ApprovalNode = {
      nodeId: node.id,
      nodeType: node.nodeType,
      nodeName: node.nodeName,
      childNode: childNode ? this.treeNodeToDefinition(childNode) : undefined
    };

    if (node.nodeType === 'approve') {
      const approveNode = node as ApproveNode;
      base.approverConfig = approveNode.approverConfig ?? {
        setType: approveNode.assigneeType ?? 0,
        signType: approveNode.approvalMode === 'sequential' ? 3 : approveNode.approvalMode === 'any' ? 2 : 1,
        noHeaderAction: approveNode.noHeaderAction ?? 0,
        nodeApproveList: approveNode.assigneeValue
          ? [{ targetId: approveNode.assigneeValue, name: approveNode.assigneeValue }]
          : []
      };
      base.buttonPermissionConfig = approveNode.buttonPermissionConfig;
      base.formPermissionConfig = approveNode.formPermissionConfig;
      base.noticeConfig = approveNode.noticeConfig;
      
      // 高级设置
      base.timeoutEnabled = approveNode.timeoutEnabled;
      base.timeoutHours = approveNode.timeoutHours;
      base.timeoutMinutes = approveNode.timeoutMinutes;
      base.timeoutAction = approveNode.timeoutAction;
      base.reminderIntervalHours = approveNode.reminderIntervalHours;
      base.maxReminderCount = approveNode.maxReminderCount;
      base.deduplicationType = approveNode.deduplicationType;
      base.excludeUserIds = approveNode.excludeUserIds;
      base.excludeRoleCodes = approveNode.excludeRoleCodes;
      base.callAi = approveNode.callAi;
      base.aiConfig = approveNode.aiConfig;

      // 审批策略
      base.rejectStrategy = approveNode.rejectStrategy;
      base.reApproveStrategy = approveNode.reApproveStrategy;
      base.groupStrategy = approveNode.groupStrategy;
      base.voteWeight = approveNode.voteWeight;
      base.votePassRate = approveNode.votePassRate;
      base.approveSelf = approveNode.approveSelf;
    }

    if (node.nodeType === 'copy') {
      const copyNode = node as CopyNode;
      base.copyConfig = {
        nodeApproveList: (copyNode.copyToUsers || []).map((id) => ({
          targetId: id,
          name: id
        }))
      };
      base.formPermissionConfig = copyNode.formPermissionConfig;
    }

    if (node.nodeType === 'condition' || node.nodeType === 'dynamicCondition' || node.nodeType === 'parallelCondition' || node.nodeType === 'inclusive') {
      const conditionNode = node as ConditionNode | DynamicConditionNode | ParallelConditionNode | InclusiveNode;
      base.conditionNodes = (conditionNode.conditionNodes || []).map((branch) => this.branchToDefinition(branch));
    }

    if (node.nodeType === 'parallel') {
      const parallelNode = node as ParallelNode;
      base.parallelNodes = (parallelNode.parallelNodes || []).map((child) => this.treeNodeToDefinition(child));
    }

    if (node.nodeType === 'route') {
      const routeNode = node as RouteNode;
      // 路由节点没有特殊配置，目标节点ID通常通过连线体现，或者作为属性存储
      // FlowLong 中路由节点是直接跳转，不生成任务
      // 我们需要确保 routeTargetNodeId 被保存
      // 但 ApprovalNode 定义中没有 routeTargetNodeId 字段，可能需要扩展 ApprovalNode
      // 暂时存入 properties 或 data
      // 假设后端 FlowNode 有 GetRouteTarget 方法，说明它是通过出边来判断的
      // 所以这里不需要额外保存 targetId，只要保证连线正确即可
      // 但 treeToGraph 会处理连线。treeToDefinitionJson 是给后端用的。
      // 后端解析器通过 edges 来判断路由目标。
    }

    if (node.nodeType === 'callProcess') {
        const callNode = node as CallProcessNode;
        base.callProcessId = callNode.callProcessId ? parseInt(callNode.callProcessId) : undefined;
        base.callAsync = callNode.callAsync;
    }

    if (node.nodeType === 'timer') {
        const timerNode = node as TimerNode;
        base.timerConfig = JSON.stringify(timerNode.timerConfig);
    }

    if (node.nodeType === 'trigger') {
        const triggerNode = node as TriggerNode;
        base.triggerType = triggerNode.triggerType;
        // triggerConfig ?
    }

    return base;
  }

  private static branchToDefinition(branch: ConditionBranch): DefinitionBranch {
    let conditionRule: JsonValue | undefined = branch.conditionRule as JsonValue | undefined;

    // 如果有 conditionGroups，转换为后端格式
    if (branch.conditionGroups && branch.conditionGroups.length > 0) {
      conditionRule = {
        relationship: 'OR',
        conditions: branch.conditionGroups.map(group => ({
          relationship: 'AND',
          conditions: group.conditions.map(cond => ({
            field: cond.field,
            operator: cond.operator,
            value: cond.value,
            ...(cond.fieldType ? { type: cond.fieldType } : {})
          }))
        }))
      } as JsonValue;
    }

    return {
      id: branch.id,
      branchName: branch.branchName,
      conditionRule,
      isDefault: branch.isDefault,
      childNode: branch.childNode ? this.treeNodeToDefinition(branch.childNode) : undefined
    };
  }

  private static definitionNodeToTree(node: ApprovalNode): TreeNode {
    const base = {
      id: node.nodeId,
      nodeType: node.nodeType,
      nodeName: node.nodeName
    };

    switch (node.nodeType) {
      case 'approve': {
        const config = node.approverConfig;
        const assigneeValue = config?.nodeApproveList?.[0]?.targetId ?? '';
        return {
          ...base,
          nodeType: 'approve',
          assigneeType: config?.setType ?? 0,
          assigneeValue,
          approvalMode: config?.signType === 3 ? 'sequential' : config?.signType === 2 ? 'any' : 'all',
          noHeaderAction: config?.noHeaderAction ?? 0,
          approverConfig: config ?? undefined,
          buttonPermissionConfig: node.buttonPermissionConfig,
          formPermissionConfig: node.formPermissionConfig,
          noticeConfig: node.noticeConfig,
          childNode: node.childNode ? this.definitionNodeToTree(node.childNode) : undefined,
          
          // 高级设置
          timeoutEnabled: node.timeoutEnabled,
          timeoutHours: node.timeoutHours,
          timeoutMinutes: node.timeoutMinutes,
          timeoutAction: node.timeoutAction,
          reminderIntervalHours: node.reminderIntervalHours,
          maxReminderCount: node.maxReminderCount,
          deduplicationType: node.deduplicationType,
          excludeUserIds: node.excludeUserIds,
          excludeRoleCodes: node.excludeRoleCodes,
          callAi: node.callAi,
          aiConfig: node.aiConfig,

          // 审批策略
          rejectStrategy: node.rejectStrategy as ApproveNode['rejectStrategy'],
          reApproveStrategy: node.reApproveStrategy as ApproveNode['reApproveStrategy'],
          groupStrategy: node.groupStrategy as ApproveNode['groupStrategy'],
          voteWeight: node.voteWeight,
          votePassRate: node.votePassRate,
          approveSelf: node.approveSelf as ApproveNode['approveSelf']
        } as ApproveNode;
      }
      case 'copy':
        return {
          ...base,
          nodeType: 'copy',
          copyToUsers: (node.copyConfig?.nodeApproveList || []).map((item) => item.targetId),
          formPermissionConfig: node.formPermissionConfig,
          childNode: node.childNode ? this.definitionNodeToTree(node.childNode) : undefined
        } as CopyNode;
      case 'condition':
        return {
          ...base,
          nodeType: 'condition',
          conditionNodes: (node.conditionNodes || []).map((branch) => this.definitionBranchToTree(branch)),
          childNode: node.childNode ? this.definitionNodeToTree(node.childNode) : undefined
        } as ConditionNode;
      case 'dynamicCondition':
        return {
          ...base,
          nodeType: 'dynamicCondition',
          conditionNodes: (node.conditionNodes || []).map((branch) => this.definitionBranchToTree(branch)),
          childNode: node.childNode ? this.definitionNodeToTree(node.childNode) : undefined
        } as DynamicConditionNode;
      case 'parallelCondition':
        return {
          ...base,
          nodeType: 'parallelCondition',
          conditionNodes: (node.conditionNodes || []).map((branch) => this.definitionBranchToTree(branch)),
          childNode: node.childNode ? this.definitionNodeToTree(node.childNode) : undefined
        } as ParallelConditionNode;
      case 'parallel':
        return {
          ...base,
          nodeType: 'parallel',
          parallelNodes: (node.parallelNodes || []).map((child) => this.definitionNodeToTree(child)),
          childNode: node.childNode ? this.definitionNodeToTree(node.childNode) : undefined
        } as ParallelNode;
      case 'inclusive':
        return {
          ...base,
          nodeType: 'inclusive',
          conditionNodes: (node.conditionNodes || []).map((branch) => this.definitionBranchToTree(branch)),
          childNode: node.childNode ? this.definitionNodeToTree(node.childNode) : undefined
        } as InclusiveNode;
      case 'route':
        return {
            ...base,
            nodeType: 'route',
            // routeTargetNodeId 从边获取，这里无法直接获取，需要在 graphToTree 中处理
        } as RouteNode;
      case 'callProcess':
        return {
            ...base,
            nodeType: 'callProcess',
            callProcessId: node.callProcessId?.toString(),
            callAsync: node.callAsync
        } as CallProcessNode;
      case 'timer':
        return {
            ...base,
            nodeType: 'timer',
            timerConfig: node.timerConfig ? JSON.parse(node.timerConfig) : undefined
        } as TimerNode;
      case 'trigger':
        return {
            ...base,
            nodeType: 'trigger',
            triggerType: node.triggerType as any
        } as TriggerNode;
      case 'start':
        return {
          ...base,
          nodeType: 'start',
          childNode: node.childNode ? this.definitionNodeToTree(node.childNode) : undefined
        } as StartNode;
      case 'end':
        return { ...base, nodeType: 'end' } as EndNode;
      default:
        return { ...base, nodeType: 'end' } as EndNode;
    }
  }

  private static definitionBranchToTree(branch: DefinitionBranch): ConditionBranch {
    const treeBranch: ConditionBranch = {
      id: branch.id,
      branchName: branch.branchName,
      isDefault: branch.isDefault,
      childNode: branch.childNode ? this.definitionNodeToTree(branch.childNode) : undefined
    };

    // 尝试解析 conditionRule 为 conditionGroups
    if (branch.conditionRule) {
      const rule = branch.conditionRule as any;
      // 检查是否为复杂结构
      if (rule.relationship === 'OR' && Array.isArray(rule.conditions)) {
        treeBranch.conditionGroups = rule.conditions.map((group: any) => ({
          conditions: Array.isArray(group.conditions) ? group.conditions.map((cond: any) => ({
            field: cond.field,
            operator: cond.operator,
            value: cond.value,
            fieldType: cond.type
          })) : []
        }));
      } else if (rule.field && rule.operator) {
        // 简单结构，兼容旧数据
        treeBranch.conditionRule = {
          field: rule.field,
          operator: rule.operator,
          value: rule.value
        };
        // 同时生成一个默认 group
        treeBranch.conditionGroups = [{
          conditions: [{
            field: rule.field,
            operator: rule.operator,
            value: rule.value
          }]
        }];
      }
    }

    return treeBranch;
  }
  
  /**
   * 查找条件分支的汇聚点（多源 BFS：所有分支均能到达的最近节点）
   */
  private static findMergePoint(
    branchStartIds: string[],
    outgoing: Map<string, GraphEdge[]>
  ): string | null {
    if (branchStartIds.length < 2) return null;

    type QueueItem = { nodeId: string; branchIdx: number };
    const visitedPerBranch: Set<string>[] = branchStartIds.map(() => new Set());
    const branchesReachedNode = new Map<string, Set<number>>();
    const queue: QueueItem[] = branchStartIds.map((startId, idx) => ({ nodeId: startId, branchIdx: idx }));

    while (queue.length > 0) {
      const item = queue.shift()!;
      const { nodeId, branchIdx } = item;

      if (visitedPerBranch[branchIdx].has(nodeId)) continue;
      visitedPerBranch[branchIdx].add(nodeId);

      if (!branchesReachedNode.has(nodeId)) {
        branchesReachedNode.set(nodeId, new Set());
      }
      branchesReachedNode.get(nodeId)!.add(branchIdx);

      // 所有分支均到达且不是分支起点本身 → 汇聚点
      if (
        branchesReachedNode.get(nodeId)!.size === branchStartIds.length &&
        !branchStartIds.includes(nodeId)
      ) {
        return nodeId;
      }

      const edges = outgoing.get(nodeId) ?? [];
      for (const edge of edges) {
        const targetId = getEdgeTarget(edge.target);
        if (targetId && !visitedPerBranch[branchIdx].has(targetId)) {
          queue.push({ nodeId: targetId, branchIdx });
        }
      }
    }
    return null;
  }

  // 私有辅助方法：从图构建树
  private static buildTreeFromNode(
    nodeId: string,
    allNodes: GraphNode[],
    outgoing: Map<string, GraphEdge[]>,
    stopAtNodeId?: string
  ): TreeNode {
    // 汇聚点：返回 end 占位，由父节点接管后续树
    if (stopAtNodeId && nodeId === stopAtNodeId) {
      return { id: nodeId, nodeType: 'end', nodeName: '结束' } as EndNode;
    }

    const nodeData = allNodes.find(n => n.id === nodeId);
    if (!nodeData) throw new Error(`Node ${nodeId} not found`);

    const data = (nodeData.data ?? {}) as GraphNodeData;
    const type = data.type || nodeData.type;
    const normalizedType = normalizeNodeType(type);
    const resolvedNodeId = nodeData.id ?? nanoid();
    const base = {
        id: resolvedNodeId,
        nodeType: normalizedType,
        nodeName: data.label || nodeData.label || '未命名',
    };

    // 获取出边
    const edges = outgoing.get(resolvedNodeId) || [];

    if (normalizedType === 'start') {
        const node: StartNode = { ...base, nodeType: 'start' };
        if (edges.length > 0) {
            // 假设 Start 只有一个后续
            const targetId = getEdgeTarget(edges[0].target);
            if (targetId) {
              node.childNode = this.buildTreeFromNode(targetId, allNodes, outgoing);
            }
        }
        return node;
    } else if (normalizedType === 'approve') {
        const node: ApproveNode = {
            ...base,
            nodeType: 'approve',
            assigneeType: normalizeAssigneeType(data.assigneeType),
            assigneeValue: data.assigneeValue ?? '',
            approvalMode: normalizeApprovalMode(data.approvalMode)
        };
        if (edges.length > 0) {
            // 假设 Approve 只有一个后续（除非接了条件节点，但条件节点也是一个节点）
             const targetId = getEdgeTarget(edges[0].target);
             if (targetId) {
               node.childNode = this.buildTreeFromNode(targetId, allNodes, outgoing);
             }
        }
        return node;
    } else if (normalizedType === 'copy') {
        const node: CopyNode = {
            ...base,
            nodeType: 'copy',
            copyToUsers: data.copyToUsers || []
        };
        if (edges.length > 0) {
             const targetId = getEdgeTarget(edges[0].target);
             if (targetId) {
               node.childNode = this.buildTreeFromNode(targetId, allNodes, outgoing);
             }
        }
        return node;
    } else if (normalizedType === 'condition' || normalizedType === 'dynamicCondition' || normalizedType === 'parallelCondition') {
        // 这里的逻辑比较复杂。
        // 在 X6 图中，Condition 节点可能有多个出边，每个出边代表一个分支。
        // 我们需要识别这些分支，以及它们是否汇聚。
        
        const node: ConditionNode | DynamicConditionNode | ParallelConditionNode = {
            ...base,
            nodeType: normalizedType,
            conditionNodes: []
        } as ConditionNode;

        // 查找汇聚点：多源 BFS 找所有分支均可到达的最近节点
        const branchStartIds = edges.map(e => getEdgeTarget(e.target)).filter(Boolean);
        const mergeNodeId = this.findMergePoint(branchStartIds, outgoing);

        const branches: ConditionBranch[] = edges.map((edge, index) => {
            const targetId = getEdgeTarget(edge.target);
            const branchRoot = this.buildTreeFromNode(targetId, allNodes, outgoing, mergeNodeId ?? undefined);
            return {
                id: nanoid(),
                branchName: typeof edge.label === 'string' ? edge.label : `条件${index + 1}`,
                conditionRule: (edge.data as GraphEdgeData | undefined)?.conditionRule,
                childNode: branchRoot
            };
        });

        node.conditionNodes = branches;

        // 将汇聚点提升为 conditionNode.childNode（已在后面 if(mergeNodeId) 处理）
        // 在图转树的过程中，如果多个分支最终指向同一个节点，那个节点就是汇聚点。
        // 简单的递归 buildTreeFromNode 会导致汇聚点被重复构建在每个分支的末尾。
        // 这是一个图转树的经典问题。
        // 鉴于 AntFlow 的树结构设计，我们可能需要一种策略来“剪断”汇聚点，将其提升为 conditionNode.childNode。
        
        // 简化策略：
        // 1. 遍历所有分支，看它们的子孙节点是否有相同的 ID。
        // 2. 如果有，提取出来作为 conditionNode.childNode。
        
        // 由于时间限制，且通常设计器生成的图结构比较规范。
        // 我们假设：如果所有分支的路径最终都汇聚到同一个节点 X，则 X 是 conditionNode.childNode。
        // 这里暂时不实现复杂的图算法，而是假设 graphToTree 主要用于从简单的 definitionJson 恢复。
        // 将汇聚点提升为 conditionNode.childNode，继续构建后续树
        if (mergeNodeId) {
            node.childNode = this.buildTreeFromNode(mergeNodeId, allNodes, outgoing, stopAtNodeId);
        }

        return node as TreeNode;
    } else if (normalizedType === 'parallel') {
        const node: ParallelNode = {
            ...base,
            nodeType: 'parallel',
            parallelNodes: edges.map((edge) => {
                const targetId = getEdgeTarget(edge.target);
                return targetId ? this.buildTreeFromNode(targetId, allNodes, outgoing, stopAtNodeId) : this.createDefaultTree().rootNode;
            })
        };
        return node;
    } else if (normalizedType === 'inclusive') {
        const node: InclusiveNode = {
            ...base,
            nodeType: 'inclusive',
            conditionNodes: []
        };
        // 类似 ConditionNode 处理，同样需要汇聚点检测
        const inclusiveBranchStartIds = edges.map(e => getEdgeTarget(e.target)).filter(Boolean);
        const inclusiveMergeNodeId = this.findMergePoint(inclusiveBranchStartIds, outgoing);
        const inclusiveBranches: ConditionBranch[] = edges.map((edge, index) => {
            const targetId = getEdgeTarget(edge.target);
            const branchRoot = this.buildTreeFromNode(targetId, allNodes, outgoing, inclusiveMergeNodeId ?? undefined);
            return {
                id: nanoid(),
                branchName: typeof edge.label === 'string' ? edge.label : `条件${index + 1}`,
                conditionRule: (edge.data as GraphEdgeData | undefined)?.conditionRule,
                childNode: branchRoot
            };
        });
        node.conditionNodes = inclusiveBranches;
        if (inclusiveMergeNodeId) {
            node.childNode = this.buildTreeFromNode(inclusiveMergeNodeId, allNodes, outgoing, stopAtNodeId);
        }
        return node;
    } else if (normalizedType === 'route') {
        const node: RouteNode = {
            ...base,
            nodeType: 'route',
            routeTargetNodeId: edges.length > 0 ? getEdgeTarget(edges[0].target) : undefined
        };
        return node;
    } else if (normalizedType === 'callProcess') {
        const node: CallProcessNode = {
            ...base,
            nodeType: 'callProcess',
            callProcessId: data.callProcessId as string,
            callAsync: data.callAsync as boolean
        };
        if (edges.length > 0) {
             const targetId = getEdgeTarget(edges[0].target);
             if (targetId) {
               node.childNode = this.buildTreeFromNode(targetId, allNodes, outgoing);
             }
        }
        return node;
    } else if (normalizedType === 'timer') {
        const node: TimerNode = {
            ...base,
            nodeType: 'timer',
            timerConfig: data.timerConfig as any
        };
        if (edges.length > 0) {
             const targetId = getEdgeTarget(edges[0].target);
             if (targetId) {
               node.childNode = this.buildTreeFromNode(targetId, allNodes, outgoing);
             }
        }
        return node;
    } else if (normalizedType === 'trigger') {
        const node: TriggerNode = {
            ...base,
            nodeType: 'trigger',
            triggerType: data.triggerType as any
        };
        if (edges.length > 0) {
             const targetId = getEdgeTarget(edges[0].target);
             if (targetId) {
               node.childNode = this.buildTreeFromNode(targetId, allNodes, outgoing);
             }
        }
        return node;
    } else if (normalizedType === 'end') {
        return { ...base, nodeType: 'end' } as EndNode;
    }

    return { ...base, nodeType: 'end' } as EndNode;
  }
}

type GraphNodeData = {
  id: string;
  type: NodeType;
  label?: string;
  x?: number;
  y?: number;
  assigneeType?: ApproveNode['assigneeType'];
  assigneeValue?: string;
  approvalMode?: ApproveNode['approvalMode'];
  copyToUsers?: string[];
  [key: string]: JsonValue | undefined;
};

type GraphNode = Node.Metadata;
type GraphEdge = Edge.Metadata;
type GraphEdgeData = {
  conditionRule?: ConditionBranch['conditionRule'];
} & Record<string, JsonValue>;

const getEdgeTarget = (target: Edge.Metadata['target']): string => {
  const cellId = getTerminalCellId(target);
  return cellId ?? '';
};

const getTerminalCellId = (terminal: Edge.Metadata['source'] | Edge.Metadata['target']): string | null => {
  if (!terminal) return null;
  if (typeof terminal === 'string') return terminal;
  if (typeof terminal === 'object' && 'cell' in terminal) {
    if (typeof terminal.cell === 'string') return terminal.cell;
    return terminal.cell?.id ?? null;
  }
  return null;
};

const isAssigneeType = (value: number | undefined): value is ApproveNode['assigneeType'] => {
  return value === 0
    || value === 1
    || value === 2
    || value === 3
    || value === 4
    || value === 5
    || value === 6
    || value === 7
    || value === 8
    || value === 9
    || value === 10;
};

const normalizeAssigneeType = (value: number | undefined): ApproveNode['assigneeType'] => {
  return isAssigneeType(value) ? value : 0;
};

const isApprovalMode = (value: string | undefined): value is ApproveNode['approvalMode'] => {
  return value === 'all' || value === 'any' || value === 'sequential' || value === 'vote';
};

const normalizeApprovalMode = (value: string | undefined): ApproveNode['approvalMode'] => {
  return isApprovalMode(value) ? value : 'all';
};

const isNodeType = (value: string | undefined): value is NodeType => {
  return value === 'start'
    || value === 'approve'
    || value === 'copy'
    || value === 'condition'
    || value === 'parallel'
    || value === 'dynamicCondition'
    || value === 'parallelCondition'
    || value === 'inclusive'
    || value === 'route'
    || value === 'callProcess'
    || value === 'timer'
    || value === 'trigger'
    || value === 'end';
};

const normalizeNodeType = (value: string | undefined): NodeType => {
  return isNodeType(value) ? value : 'end';
};

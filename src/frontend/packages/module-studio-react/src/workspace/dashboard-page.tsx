import React, { useCallback } from "react";
import {
  Button,
  Card,
  List,
  Space,
  Typography,
  Avatar,
  Tag,
  Empty
} from "@douyinfe/semi-ui";
import {
  IconUserCircle,
  IconAppCenter,
  IconFlowChartStroked,
  IconBox,
  IconSetting,
  IconGlobe,
  IconUploadError
} from "@douyinfe/semi-icons";
import type { StudioPageProps, DashboardStats, WorkspaceIdeResource } from "../types";
import { usePageState, PageStateWrapper, useStudioContext } from "../shared";
import { QuickStartCard } from "./quick-start-card";
import { ModelGuardBanner } from "./model-guard-banner";

export interface DashboardPageProps extends StudioPageProps {
  onNavigateToResource?: (resourceType: string, resourceId: string) => void;
  onNavigateToModels?: () => void;
  onNavigateToPublish?: () => void;
  onCreateAgent?: () => void;
  onCreateApp?: () => void;
  onCreateWorkflow?: () => void;
}

export function DashboardPage({
  api,
  locale,
  onNavigateToResource,
  onNavigateToModels,
  onNavigateToPublish,
  onCreateAgent,
  onCreateApp,
  onCreateWorkflow
}: DashboardPageProps) {
  const { hasEnabledModel } = useStudioContext();

  const loadDashboardData = useCallback(async () => {
    return await api.getDashboardStats();
  }, [api]);

  const { status, data, error, reload } = usePageState<DashboardStats>(loadDashboardData);

  const renderResourceIcon = (type: string) => {
    switch (type) {
      case "agent": return <IconUserCircle />;
      case "app": return <IconAppCenter />;
      case "workflow": return <IconFlowChartStroked />;
      case "plugin": return <IconBox />;
      default: return <IconSetting />;
    }
  };

  const getResourceColor = (type: string) => {
    switch (type) {
      case "agent": return "blue";
      case "app": return "green";
      case "workflow": return "purple";
      case "plugin": return "orange";
      default: return "grey";
    }
  };

  const getResourceLabel = (type: string) => {
    switch (type) {
      case "agent": return locale === "en-US" ? "Agent" : "智能体";
      case "app": return locale === "en-US" ? "Application" : "应用";
      case "workflow": return locale === "en-US" ? "Workflow" : "工作流";
      case "plugin": return locale === "en-US" ? "Plugin" : "插件";
      case "knowledge-base": return locale === "en-US" ? "Knowledge Base" : "知识库";
      default: return locale === "en-US" ? "Resource" : "资源";
    }
  };

  const formatTimestamp = (value: string) => {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return locale === "en-US" ? "Unknown time" : "时间未知";
    }

    return date.toLocaleString(locale === "en-US" ? "en-US" : "zh-CN", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit"
    });
  };

  const workspaceStats = data ? [
    {
      key: "agent",
      label: locale === "en-US" ? "Agents" : "智能体",
      description: locale === "en-US" ? "Reusable AI assistants" : "可持续调试与发布的 AI 助手",
      value: data.agentCount,
      color: "blue",
      icon: <IconUserCircle size="large" />
    },
    {
      key: "app",
      label: locale === "en-US" ? "Applications" : "应用",
      description: locale === "en-US" ? "Deliverable AI experiences" : "对外交付的业务入口与应用壳",
      value: data.appCount,
      color: "green",
      icon: <IconAppCenter size="large" />
    },
    {
      key: "workflow",
      label: locale === "en-US" ? "Workflows" : "工作流",
      description: locale === "en-US" ? "Runnable orchestration assets" : "可编排的流程与自动化资产",
      value: data.workflowCount,
      color: "purple",
      icon: <IconFlowChartStroked size="large" />
    },
    {
      key: "plugin",
      label: locale === "en-US" ? "Plugins" : "插件",
      description: locale === "en-US" ? "Callable tool capabilities" : "可被 Agent 调用的工具能力",
      value: data.pluginCount,
      color: "orange",
      icon: <IconBox size="large" />
    },
    {
      key: "knowledge-base",
      label: locale === "en-US" ? "Knowledge Bases" : "知识库",
      description: locale === "en-US" ? "Shared grounding data" : "为应用与工作流复用的数据底座",
      value: data.knowledgeBaseCount,
      color: "cyan",
      icon: <IconSetting size="large" />
    },
    {
      key: "model",
      label: locale === "en-US" ? "Enabled Models" : "可用模型",
      description: locale === "en-US" ? "Available model providers" : "已启用并可供运行的模型",
      value: data.enabledModelCount,
      color: hasEnabledModel ? "cyan" : "red",
      icon: <IconGlobe size="large" />
    }
  ] : [];

  return (
    <div className="module-studio__dashboard" data-testid="app-dashboard-page">
      <PageStateWrapper status={status} error={error} onRetry={reload}>
        {data && (
          <Space vertical align="start" style={{ width: "100%" }} spacing={24}>
            <section className="module-studio__dashboard-hero">
              <div className="module-studio__dashboard-hero-copy">
                <Tag color="indigo" size="large">Workspace Home</Tag>
                <Typography.Title heading={2} style={{ margin: 0 }}>
                  AI Studio 工作台
                </Typography.Title>
                <Typography.Text type="tertiary" className="module-studio__dashboard-hero-text">
                  像 Coze 的工作空间首页一样，把创建入口、继续工作和发布状态收敛到一个总览页里。
                  左侧负责模块导航，这里只保留当前工作区最需要关注的动作与状态。
                </Typography.Text>
                <Space wrap className="module-studio__dashboard-hero-actions">
                  {onNavigateToPublish ? (
                    <Button theme="solid" type="primary" onClick={onNavigateToPublish}>
                      查看发布中心
                    </Button>
                  ) : null}
                  {onNavigateToModels ? (
                    <Button onClick={onNavigateToModels}>模型配置</Button>
                  ) : null}
                </Space>
              </div>

              <div className="module-studio__dashboard-hero-side">
                <div className="module-studio__dashboard-status-card">
                  <span className="module-studio__dashboard-status-label">模型状态</span>
                  <strong>{hasEnabledModel ? "已可用" : "待配置"}</strong>
                  <p>
                    {hasEnabledModel
                      ? `当前已有 ${data.enabledModelCount} 个模型可供调试与运行。`
                      : "当前还没有可运行的模型，智能体与工作流无法进入完整调试流程。"}
                  </p>
                </div>
                <div className="module-studio__dashboard-status-card">
                  <span className="module-studio__dashboard-status-label">发布状态</span>
                  <strong>{data.pendingPublishItems.length} 项待处理</strong>
                  <p>
                    {data.pendingPublishItems.length > 0
                      ? "草稿变更尚未进入发布链路，建议优先处理待发布资源。"
                      : "当前工作区没有待发布变更，可以继续迭代新功能。"}
                  </p>
                </div>
              </div>
            </section>

            <ModelGuardBanner locale={locale} onConfigureModels={onNavigateToModels} />

            <QuickStartCard
              locale={locale}
              onCreateAgent={onCreateAgent}
              onCreateApp={onCreateApp}
              onCreateWorkflow={onCreateWorkflow}
            />

            <section className="module-studio__dashboard-section">
              <div className="module-studio__dashboard-section-head">
                <div>
                  <Typography.Title heading={5} style={{ margin: 0 }}>
                    工作区总览
                  </Typography.Title>
                  <Typography.Text type="tertiary">
                    快速了解当前空间里的核心资产规模，不在首页重复堆叠模块入口。
                  </Typography.Text>
                </div>
              </div>
              <div className="module-studio__dashboard-stats-grid">
                {workspaceStats.map(item => (
                  <Card key={item.key} className="module-studio__dashboard-stat-card" bordered={false}>
                    <div className="module-studio__dashboard-stat-content">
                      <div>
                        <Typography.Text type="tertiary">{item.label}</Typography.Text>
                        <Typography.Title
                          heading={3}
                          style={{
                            marginTop: 8,
                            marginBottom: 4,
                            color: item.key === "model" && !hasEnabledModel ? "var(--semi-color-danger)" : undefined
                          }}
                        >
                          {item.value}
                        </Typography.Title>
                        <Typography.Text size="small" type="tertiary">
                          {item.description}
                        </Typography.Text>
                      </div>
                      <Avatar color={item.color as never} size="large" shape="square" style={{ borderRadius: 8 }}>
                        {item.icon}
                      </Avatar>
                    </div>
                  </Card>
                ))}
              </div>
            </section>

            <div className="module-studio__dashboard-main">
              <Card
                bordered={false}
                className="module-studio__dashboard-panel"
                title={
                  <div className="module-studio__dashboard-panel-title">
                    <Typography.Title heading={5} style={{ margin: 0 }}>
                      继续工作
                    </Typography.Title>
                    <Typography.Text type="tertiary">
                      最近访问的资源会在这里聚合，方便直接回到上次中断的位置。
                    </Typography.Text>
                  </div>
                }
              >
                <List
                  dataSource={data.recentActivities}
                  emptyContent={<Empty title="暂无访问记录" image={<IconBox style={{ fontSize: 48, color: "var(--semi-color-tertiary)" }} />} />}
                  renderItem={item => (
                    <List.Item
                      className="module-studio__dashboard-list-item"
                      onClick={() => onNavigateToResource?.(item.resourceType, item.resourceId)}
                      main={
                        <div className="module-studio__dashboard-list-main">
                          <Avatar color={getResourceColor(item.resourceType) as never} size="small" shape="square" style={{ borderRadius: 6 }}>
                            {renderResourceIcon(item.resourceType)}
                          </Avatar>
                          <div className="module-studio__dashboard-list-copy">
                            <div className="module-studio__dashboard-list-head">
                              <span>{item.name}</span>
                              <Tag size="small" color="white">{getResourceLabel(item.resourceType)}</Tag>
                              {item.publishStatus?.toLowerCase() === "published" ? <Tag size="small" color="green">已发布</Tag> : null}
                            </div>
                            <Typography.Text size="small" type="tertiary">
                              最后编辑于 {formatTimestamp(item.updatedAt)}
                            </Typography.Text>
                          </div>
                        </div>
                      }
                    />
                  )}
                />
              </Card>

              <Card
                bordered={false}
                className="module-studio__dashboard-panel module-studio__dashboard-panel--side"
                title={
                  <div className="module-studio__dashboard-panel-title">
                    <Typography.Title heading={5} style={{ margin: 0 }}>
                      发布与状态
                    </Typography.Title>
                    <Typography.Text type="tertiary">
                      首页只保留当前需要处理的状态，不再重复展示完整模块目录。
                    </Typography.Text>
                  </div>
                }
                headerExtraContent={
                  data.pendingPublishItems.length > 0 && onNavigateToPublish ? (
                    <Button type="primary" theme="borderless" onClick={onNavigateToPublish}>
                      前往发布中心
                    </Button>
                  ) : undefined
                }
              >
                <div className="module-studio__dashboard-side-metrics">
                  <div className="module-studio__dashboard-side-metric">
                    <span>模型可用性</span>
                    <strong>{hasEnabledModel ? "已满足运行条件" : "需要先启用模型"}</strong>
                  </div>
                  <div className="module-studio__dashboard-side-metric">
                    <span>待发布更新</span>
                    <strong>{data.pendingPublishItems.length}</strong>
                  </div>
                </div>

                <List
                  dataSource={data.pendingPublishItems}
                  emptyContent={
                    <div className="module-studio__dashboard-empty-state">
                      <Typography.Text type="tertiary">所有修改均已发布</Typography.Text>
                    </div>
                  }
                  renderItem={item => (
                    <List.Item
                      className="module-studio__dashboard-list-item"
                      onClick={() => onNavigateToResource?.(item.resourceType, item.resourceId)}
                      main={
                        <div className="module-studio__dashboard-pending-item">
                          <IconUploadError style={{ color: "var(--semi-color-warning)" }} />
                          <div className="module-studio__dashboard-list-copy">
                            <Typography.Text ellipsis={{ showTooltip: true }} style={{ width: "100%", fontWeight: 500 }}>
                              {item.resourceName}
                            </Typography.Text>
                            <Typography.Text size="small" type="tertiary">
                              {getResourceLabel(item.resourceType)} · 更新于 {formatTimestamp(item.updatedAt)}
                            </Typography.Text>
                          </div>
                        </div>
                      }
                    />
                  )}
                />
              </Card>
            </div>
          </Space>
        )}
      </PageStateWrapper>
    </div>
  );
}

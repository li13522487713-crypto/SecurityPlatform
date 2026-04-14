import React, { useCallback, useState } from "react";
import { 
  Button, 
  Card, 
  Col, 
  List, 
  Row, 
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

  return (
    <div className="module-studio__dashboard">
      <div className="module-studio__dashboard-header">
        <div>
          <Typography.Title heading={3}>AI Studio 工作台</Typography.Title>
          <Typography.Text type="tertiary">一站式 AI 智能体与工作流开发平台</Typography.Text>
        </div>
      </div>

      <ModelGuardBanner onConfigureModels={onNavigateToModels} />

      <PageStateWrapper status={status} error={error} onRetry={reload}>
        {data && (
          <Space vertical align="start" style={{ width: "100%" }} spacing={24}>
            {/* Quick Start Section */}
            <QuickStartCard 
              onCreateAgent={onCreateAgent}
              onCreateApp={onCreateApp}
              onCreateWorkflow={onCreateWorkflow}
            />

            {/* Overview Stats Section */}
            <Row gutter={16} className="module-studio__dashboard-stats">
              <Col span={6}>
                <Card className="module-studio__dashboard-stat-card" bordered={false}>
                  <div className="module-studio__dashboard-stat-content">
                    <div>
                      <Typography.Text type="tertiary">智能体 (Agents)</Typography.Text>
                      <Typography.Title heading={2} style={{ marginTop: 8 }}>{data.agentCount}</Typography.Title>
                    </div>
                    <Avatar color="blue" size="large" shape="square" style={{ borderRadius: 8 }}>
                      <IconUserCircle size="large" />
                    </Avatar>
                  </div>
                </Card>
              </Col>
              <Col span={6}>
                <Card className="module-studio__dashboard-stat-card" bordered={false}>
                  <div className="module-studio__dashboard-stat-content">
                    <div>
                      <Typography.Text type="tertiary">应用 (Apps)</Typography.Text>
                      <Typography.Title heading={2} style={{ marginTop: 8 }}>{data.appCount}</Typography.Title>
                    </div>
                    <Avatar color="green" size="large" shape="square" style={{ borderRadius: 8 }}>
                      <IconAppCenter size="large" />
                    </Avatar>
                  </div>
                </Card>
              </Col>
              <Col span={6}>
                <Card className="module-studio__dashboard-stat-card" bordered={false}>
                  <div className="module-studio__dashboard-stat-content">
                    <div>
                      <Typography.Text type="tertiary">工作流 (Workflows)</Typography.Text>
                      <Typography.Title heading={2} style={{ marginTop: 8 }}>{data.workflowCount}</Typography.Title>
                    </div>
                    <Avatar color="purple" size="large" shape="square" style={{ borderRadius: 8 }}>
                      <IconFlowChartStroked size="large" />
                    </Avatar>
                  </div>
                </Card>
              </Col>
              <Col span={6}>
                <Card className="module-studio__dashboard-stat-card" bordered={false}>
                  <div className="module-studio__dashboard-stat-content">
                    <div>
                      <Typography.Text type="tertiary">可用模型</Typography.Text>
                      <Typography.Title heading={2} style={{ marginTop: 8, color: hasEnabledModel ? "inherit" : "var(--semi-color-danger)" }}>
                        {data.enabledModelCount}
                      </Typography.Title>
                    </div>
                    <Avatar color={hasEnabledModel ? "cyan" : "red"} size="large" shape="square" style={{ borderRadius: 8 }}>
                      <IconGlobe size="large" />
                    </Avatar>
                  </div>
                </Card>
              </Col>
            </Row>

            {/* Two Column Layout for Lists */}
            <Row gutter={24} style={{ width: "100%" }}>
              {/* Recent Activities */}
              <Col span={16}>
                <Card 
                  title="最近访问" 
                  bordered={false}
                  headerExtraContent={<Button type="tertiary" theme="borderless">查看全部</Button>}
                >
                  <List
                    dataSource={data.recentActivities}
                    emptyContent={<Empty title="暂无访问记录" image={<IconBox style={{ fontSize: 48, color: "var(--semi-color-tertiary)" }} />} />}
                    renderItem={item => (
                      <List.Item
                        style={{ cursor: "pointer" }}
                        onClick={() => onNavigateToResource?.(item.resourceType, item.resourceId)}
                        main={
                          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                            <Avatar color={getResourceColor(item.resourceType) as any} size="small" shape="square" style={{ borderRadius: 6 }}>
                              {renderResourceIcon(item.resourceType)}
                            </Avatar>
                            <div>
                              <div style={{ fontWeight: 500, display: "flex", alignItems: "center", gap: 8 }}>
                                {item.name}
                                {item.publishStatus?.toLowerCase() === "published" && <Tag size="small" color="green">已发布</Tag>}
                              </div>
                              <Typography.Text size="small" type="tertiary">
                                {item.resourceType.toUpperCase()} • 最后编辑于 {new Date(item.updatedAt).toLocaleString()}
                              </Typography.Text>
                            </div>
                          </div>
                        }
                      />
                    )}
                  />
                </Card>
              </Col>

              {/* Pending Publish */}
              <Col span={8}>
                <Card 
                  title="待发布更新" 
                  bordered={false}
                  headerExtraContent={
                    data.pendingPublishItems.length > 0 && (
                      <Button type="primary" theme="borderless" onClick={onNavigateToPublish}>
                        去发布
                      </Button>
                    )
                  }
                >
                  <List
                    dataSource={data.pendingPublishItems}
                    emptyContent={
                      <div style={{ padding: "32px 0", textAlign: "center" }}>
                        <Typography.Text type="tertiary">所有修改均已发布</Typography.Text>
                      </div>
                    }
                    renderItem={item => (
                      <List.Item
                        style={{ cursor: "pointer" }}
                        onClick={() => onNavigateToResource?.(item.resourceType, item.resourceId)}
                        main={
                          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                            <IconUploadError style={{ color: "var(--semi-color-warning)" }} />
                            <div style={{ flex: 1, minWidth: 0 }}>
                              <Typography.Text ellipsis={{ showTooltip: true }} style={{ width: "100%", fontWeight: 500 }}>
                                {item.resourceName}
                              </Typography.Text>
                              <Typography.Text size="small" type="tertiary">
                                {item.resourceType.toUpperCase()}
                              </Typography.Text>
                            </div>
                          </div>
                        }
                      />
                    )}
                  />
                </Card>
              </Col>
            </Row>
          </Space>
        )}
      </PageStateWrapper>
    </div>
  );
}

import React from "react";
import { Card, Typography } from "@douyinfe/semi-ui";
import { IconUserCircle, IconAppCenter, IconFlowChartStroked } from "@douyinfe/semi-icons";
import type { StudioLocale } from "../types";

export interface QuickStartCardProps {
  locale: StudioLocale;
  onCreateAgent?: () => void;
  onCreateApp?: () => void;
  onCreateWorkflow?: () => void;
}

export function QuickStartCard({
  locale,
  onCreateAgent,
  onCreateApp,
  onCreateWorkflow
}: QuickStartCardProps) {
  const actions = [
    {
      title: locale === "en-US" ? "Build an agent" : "构建智能体",
      description:
        locale === "en-US"
          ? "Configure prompts, tools, and knowledge bases to create a dedicated AI assistant."
          : "配置提示词、工具和知识库，创建专属 AI 助手",
      icon: <IconUserCircle size="extra-large" style={{ color: "var(--semi-color-primary)" }} />,
      onClick: onCreateAgent,
      color: "var(--semi-color-primary-light-default)"
    },
    {
      title: locale === "en-US" ? "Build an app" : "搭建应用",
      description:
        locale === "en-US"
          ? "Package workflows or agents into an interactive app with a UI."
          : "将工作流或智能体封装为带界面的交互式应用",
      icon: <IconAppCenter size="extra-large" style={{ color: "var(--semi-color-success)" }} />,
      onClick: onCreateApp,
      color: "var(--semi-color-success-light-default)"
    },
    {
      title: locale === "en-US" ? "Compose a workflow" : "编排工作流",
      description:
        locale === "en-US"
          ? "Visually connect multiple nodes to orchestrate complex AI business logic."
          : "可视化连接多个节点，编排复杂 AI 业务逻辑",
      icon: <IconFlowChartStroked size="extra-large" style={{ color: "var(--semi-color-tertiary)" }} />,
      onClick: onCreateWorkflow,
      color: "var(--semi-color-tertiary-light-default)"
    }
  ];

  return (
    <Card title={locale === "en-US" ? "Quick start" : "快速开始"} bordered={false} bodyStyle={{ padding: "0 24px 24px" }}>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: 16 }}>
        {actions.map((action, idx) => (
          <div
            key={idx}
            className="quick-start-item"
            style={{
              display: "flex",
              alignItems: "flex-start",
              padding: 20,
              borderRadius: 8,
              backgroundColor: action.color,
              cursor: "pointer",
              transition: "transform 0.2s ease, box-shadow 0.2s ease"
            }}
            onClick={action.onClick}
            onMouseEnter={e => {
              e.currentTarget.style.transform = "translateY(-2px)";
              e.currentTarget.style.boxShadow = "var(--semi-shadow-elevated)";
            }}
            onMouseLeave={e => {
              e.currentTarget.style.transform = "none";
              e.currentTarget.style.boxShadow = "none";
            }}
          >
            <div style={{
              width: 48,
              height: 48,
              borderRadius: 8,
              backgroundColor: "white",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              marginRight: 16,
              flexShrink: 0
            }}>
              {action.icon}
            </div>
            <div>
              <Typography.Title heading={6} style={{ marginBottom: 4 }}>
                {action.title}
              </Typography.Title>
              <Typography.Text type="secondary" size="small">
                {action.description}
              </Typography.Text>
            </div>
          </div>
        ))}
      </div>
    </Card>
  );
}

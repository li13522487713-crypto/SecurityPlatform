import React from "react";
import { Banner, Typography, Button } from "@douyinfe/semi-ui";
import { IconAlertTriangle } from "@douyinfe/semi-icons";
import { useStudioContext } from "../shared";

export interface ModelGuardBannerProps {
  onConfigureModels?: () => void;
}

export function ModelGuardBanner({ onConfigureModels }: ModelGuardBannerProps) {
  const { hasEnabledModel, modelConfigs } = useStudioContext();

  if (hasEnabledModel) {
    return null;
  }

  const title = modelConfigs.length === 0 
    ? "系统尚未配置 AI 模型" 
    : "当前没有已启用的 AI 模型";
    
  const description = modelConfigs.length === 0
    ? "AI Agent、工作流和应用依赖底层大语言模型才能运行。请先在模型管理中配置并启用至少一个模型提供商（如 OpenAI、千问等）。"
    : "您的模型列表中没有任何处于启用状态的模型。在启用至少一个模型之前，所有 AI 相关功能（如调试、运行）将无法正常工作。";

  return (
    <div style={{ marginBottom: 24 }}>
      <Banner
        type="warning"
        icon={<IconAlertTriangle />}
        title={<Typography.Title heading={6}>{title}</Typography.Title>}
        description={
          <div>
            <div style={{ marginBottom: 12 }}>{description}</div>
            {onConfigureModels && (
              <Button theme="solid" type="warning" onClick={onConfigureModels}>
                前往配置模型
              </Button>
            )}
          </div>
        }
      />
    </div>
  );
}

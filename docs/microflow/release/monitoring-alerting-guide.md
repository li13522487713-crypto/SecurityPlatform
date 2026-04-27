# Monitoring / Alerting Guide

## Health Signals

- `GET /api/microflows/health`
- `GET /api/microflows/storage/health`
- `GET /api/microflow-metadata/health`
- `GET /api/microflows/runtime/health`
- `GET /health/live`
- `GET /health/ready`

## Log Fields

运维日志至少保留：

- traceId
- runId
- resourceId
- workspaceId
- userId
- actionKind
- errorCode
- durationMs

禁止记录 password、token、cookie、connection string、完整敏感 payload。

## Metrics

第一版可以从结构化日志和 health 派生：

- `microflow_resource_total`
- `microflow_publish_total`
- `microflow_validation_total`
- `microflow_run_total`
- `microflow_run_duration_ms`
- `microflow_run_failed_total`
- `microflow_run_cancelled_total`
- `microflow_run_timeout_total`
- `microflow_action_failed_total`
- `microflow_connector_required_total`
- `microflow_trace_frames_total`
- `microflow_runtime_logs_total`
- `microflow_health_status`

## Alerts

- DB health unhealthy: 立即告警。
- runtime health unhealthy: 立即告警。
- run failed rate 超阈值: 5 分钟窗口告警。
- runtime timeout 高于基线: 5 分钟窗口告警。
- trace write failure: 立即告警。
- publish failure spike: 10 分钟窗口告警。
- validation service failure: 立即告警。
- connector required spike: 观察告警，通常代表试点场景超出 connector 能力。

## OpenTelemetry

仓库已有 OTel 基础设施；Round61 不引入新依赖。若配置 `OTEL_EXPORTER_OTLP_ENDPOINT`，平台 tracing 可导出。Microflow 专用 ActivitySource 建议作为 post-61 follow-up。

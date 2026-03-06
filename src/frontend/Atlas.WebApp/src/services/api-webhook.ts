import { requestApi } from '@/services/api-core'
import type { ApiResponse } from '@/types/api'

export interface WebhookSubscription {
  id: number
  name: string
  eventTypes: string[]
  targetUrl: string
  secret: string
  headers?: Record<string, string>
  isActive: boolean
  createdAt: string
  lastTriggeredAt?: string
  tenantId: string
}

export interface WebhookDeliveryLog {
  id: number
  subscriptionId: number
  eventType: string
  payload: string
  responseCode?: number
  responseBody?: string
  durationMs: number
  success: boolean
  errorMessage?: string
  createdAt: string
}

export function getWebhooks() {
  return requestApi<ApiResponse<WebhookSubscription[]>>('/webhooks')
}

export function getWebhook(id: number) {
  return requestApi<ApiResponse<WebhookSubscription>>(`/webhooks/${id}`)
}

export function createWebhook(data: {
  name: string
  eventTypes: string[]
  targetUrl: string
  secret: string
  headers?: Record<string, string>
}) {
  return requestApi<ApiResponse<{ id: number }>>('/webhooks', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  })
}

export function updateWebhook(
  id: number,
  data: {
    name: string
    eventTypes: string[]
    targetUrl: string
    isActive: boolean
    headers?: Record<string, string>
  }
) {
  return requestApi<ApiResponse<null>>(`/webhooks/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  })
}

export function deleteWebhook(id: number) {
  return requestApi<ApiResponse<null>>(`/webhooks/${id}`, {
    method: 'DELETE',
  })
}

export function getWebhookDeliveries(id: number, pageSize = 50) {
  return requestApi<ApiResponse<WebhookDeliveryLog[]>>(`/webhooks/${id}/deliveries?pageSize=${pageSize}`)
}

export function testWebhookDelivery(id: number) {
  return requestApi<ApiResponse<null>>(`/webhooks/${id}/test`, {
    method: 'POST',
  })
}

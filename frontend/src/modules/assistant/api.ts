import { http } from '../../shared/api/http'
import type { AskResponse, AssistantStatus } from './types'

export const assistantApi = {
  status() {
    return http.get<AssistantStatus>('/api/assistant/status')
  },
  // Sin conversationId empieza una nueva; la respuesta trae el id para las de seguimiento.
  ask(question: string, conversationId: string | null) {
    return http.post<AskResponse>('/api/assistant/ask', { question, conversationId })
  },
  end(conversationId: string) {
    return http.delete<void>(`/api/assistant/conversations/${conversationId}`)
  },
}

import { http } from '../../shared/api/http'
import type { AskResponse, AssistantStatus, TicketDraft } from './types'

export const assistantApi = {
  status() {
    return http.get<AssistantStatus>('/api/assistant/status')
  },
  // Sin conversationId empieza una nueva; la respuesta trae el id para las de seguimiento.
  ask(question: string, conversationId: string | null) {
    return http.post<AskResponse>('/api/assistant/ask', { question, conversationId })
  },
  // Crea el borrador de proponer_ticket solo cuando el usuario lo confirma (mismo POST que la pantalla Tickets).
  createTicket(draft: TicketDraft) {
    return http.post<{ ticketId: number }>('/api/tickets', {
      categoryId: draft.categoria_id,
      title: draft.titulo,
      description: draft.descripcion ?? undefined,
      assetId: draft.activo_id ?? undefined,
    })
  },
  end(conversationId: string) {
    return http.delete<void>(`/api/assistant/conversations/${conversationId}`)
  },
}

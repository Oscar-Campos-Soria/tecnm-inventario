// Contratos de /api/assistant (backend/Features/Assistant).

export interface AssistantStatus {
  enabled: boolean
  // Herramientas que el rol del usuario puede usar (para las sugerencias).
  tools: string[]
}

// Una consulta que hizo el asistente: qué buscó (en palabras), los datos que obtuvo o el error.
export interface AssistantStep {
  tool: string
  summary: string
  data: Record<string, unknown> | null
  error: string | null
}

// data.borrador de la herramienta proponer_ticket (nada se crea hasta que el usuario confirma).
export interface TicketDraft {
  categoria_id: number
  categoria: string
  titulo: string
  descripcion: string | null
  activo_id: number | null
  equipo: string | null
}

export interface AskResponse {
  conversationId: string
  answer: string
  steps: AssistantStep[]
}

export interface ChatMessage {
  id: number
  role: 'user' | 'assistant'
  text: string
  steps: AssistantStep[]
  failed?: boolean
}

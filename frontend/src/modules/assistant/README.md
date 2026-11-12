# Módulo assistant (asistente IA)

Botón flotante "Pregúntale" y panel de conversación, montado en `app/layouts/MainLayout.vue` para quien tiene
`assistant.use`. No tiene rutas ni entrada en el menú.

## Componentes

| Componente | Qué hace |
| --- | --- |
| `AssistantWidget.vue` | Botón, panel, conversación (con memoria en el servidor), sugerencias según las herramientas del rol, "Nueva conversación". Se oculta si `GET /api/assistant/status` dice que el modelo no responde |
| `AnswerText.vue` | Pinta el Markdown sencillo del modelo (párrafos, listas, negritas) **sin `v-html`**: las respuestas citan textos que escribieron usuarios |
| `StepEvidence.vue` | "Consulté": cada consulta en palabras y, al abrirla, la tabla con los datos reales que obtuvo |

## Endpoints

`GET /api/assistant/status`, `POST /api/assistant/ask`, `DELETE /api/assistant/conversations/{id}`.

Especificación: `specs/assistant.spec.md`

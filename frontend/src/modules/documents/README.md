# Módulo documents

Sin pantallas propias por diseño: cada archivo pertenece a un registro y se adjunta, ve y elimina ahí.

- Componente: `shared/components/DocumentList.vue` (`ownerType` + `ownerId`; `can-upload`, `can-delete`;
  sin `can-delete` solo se ofrece eliminar lo que el usuario subió). API: `shared/api/documents.ts`.
- Usos: incidentes (`INCIDENT`), tickets (`TICKET`), baja (`ASSET_DISPOSAL`, acta firmada),
  mantenimientos de la ficha del activo (`MAINTENANCE`) y detalle de compra (`ACQUISITION`).
- Consolidado por equipo: `modules/assets/components/AssetDocumentsSection.vue` (`GET /api/assets/{id}/documents`, solo lectura).

Especificación: `specs/documents.spec.md`

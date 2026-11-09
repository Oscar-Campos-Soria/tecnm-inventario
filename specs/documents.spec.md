# Spec — Documentos (Documents)

## Propósito

Gestión de evidencias y archivos asociados a registros del sistema (fotografías, facturas, actas), con control de acceso por propietario.

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `POST` | `/api/documents/upload` | `documents.create` | Sube archivo |
| `GET` | `/api/documents/owner/{ownerId}` | `documents.view` | Documentos de un propietario |
| `GET` | `/api/documents/{documentId}/download` | `documents.view` | Descarga |
| `DELETE` | `/api/documents/{documentId}` | `documents.delete` | Eliminación lógica |
| `GET` | `/api/assets/{assetId}/documents` | `assets.view` | Archivos de todos los registros del activo (incidentes, mantenimientos, baja, compras vía `acquisition_assets`, tickets), con `ownerType`, `ownerId` y `ownerLabel`. Cada dueño pasa por `IDocumentAccessAuthorizer`: lo que el usuario no puede ver se omite sin error |

## Reglas de negocio

- Almacenamiento en disco local (`backend/storage/documents/`, configurable por `FileStorage:LocalPath`).
- Acceso autorizado por `IDocumentAccessAuthorizer` (propietario o permiso de administrador).
- Metadatos en `document` (nombre, tipo MIME, tamaño, hash, propietario, entidad relacionada).

## Criterios de aceptación

- [x] Upload / descarga / listado por propietario / eliminación lógica
- [x] Vinculación polimórfica (INCIDENT, MAINTENANCE, ASSET_DISPOSAL, ACQUISITION, TICKET)
- [x] Vista consolidada en la ficha del activo (sin página global: un archivo sin su registro no da contexto)
- [x] Adjuntos en mantenimientos (ficha del activo) y compras (detalle en `/procurement`); eliminar desde `DocumentList`

## Implementación

- Feature: `backend/Features/Documents/`
- Cross-cutting: `Common/Storage/LocalDiskFileStorageService`, `Common/Authorization/DocumentAccessAuthorizer`

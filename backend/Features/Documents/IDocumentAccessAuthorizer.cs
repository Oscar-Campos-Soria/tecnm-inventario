namespace TecnmInventario.Api.Features.Documents;

public enum DocumentOperation
{
    Read,
    Upload,
    Delete
}

/// <summary>
/// Verifica si el usuario actual puede operar sobre los documentos de un dueño
/// (OwnerType + OwnerId). Primero el permiso del rol (DocumentAuthorizationMap)
/// y después, para los dueños con solicitante (TICKET, INCIDENT), que el
/// registro sea suyo cuando no tiene permisos de soporte. Compartido por
/// Upload/Get/Download/Delete.
/// </summary>
public interface IDocumentAccessAuthorizer
{
    /// <param name="documentCreatedBy">Quién subió el documento (solo para Delete).</param>
    Task EnsureAuthorizedAsync(
        string ownerType,
        long ownerId,
        DocumentOperation operation,
        long? documentCreatedBy,
        CancellationToken cancellationToken);
}

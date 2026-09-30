using Mediator;
using TecnmInventario.Api.Common.Storage;
using TecnmInventario.Infrastructure;
using TecnmInventario.Infrastructure.Entities;

namespace TecnmInventario.Api.Features.Documents.UploadDocument;

public sealed class UploadDocumentHandler : ICommandHandler<UploadDocumentCommand, UploadDocumentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentAccessAuthorizer _accessAuthorizer;
    private readonly Common.Security.ICurrentUserService _currentUser;

    public UploadDocumentHandler(
        TecnmInventarioDbContext context,
        IFileStorageService fileStorage,
        IDocumentAccessAuthorizer accessAuthorizer,
        Common.Security.ICurrentUserService currentUser)
    {
        _context = context;
        _fileStorage = fileStorage;
        _accessAuthorizer = accessAuthorizer;
        _currentUser = currentUser;
    }

    public async ValueTask<UploadDocumentResponse> Handle(
        UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        await _accessAuthorizer.EnsureAuthorizedAsync(command.OwnerType, cancellationToken);

        string storageKey;
        await using (var stream = command.File.OpenReadStream())
        {
            storageKey = await _fileStorage.SaveAsync(stream, command.File.FileName, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var document = new Document
        {
            OwnerType = command.OwnerType,
            OwnerId = command.OwnerId,
            FileName = command.File.FileName,
            StorageKey = storageKey,
            MimeType = command.File.ContentType,
            SizeBytes = command.File.Length,
            Description = command.Description,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        _context.Documents.Add(document);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _fileStorage.DeleteAsync(storageKey, cancellationToken);
            throw;
        }

        return new UploadDocumentResponse(
            document.DocumentId, document.OwnerType, document.OwnerId,
            document.FileName, document.MimeType, document.SizeBytes);
    }
}

using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Data;

public partial class TecnmInventarioDbContext
{
    // Tablas de sesión (scripts/add-auth-sessions.sql). Se mapean aquí, no en el
    // archivo generado, para no editar el scaffold. Si se vuelve a hacer scaffold
    // con estas tablas, excluirlas (-t) o borrar este mapeo para no duplicarlo.
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<UserExternalLogin> UserExternalLogins { get; set; }

    // Mesa de ayuda (scripts/add-tickets.sql). Mismo criterio que arriba:
    // mapeo a mano, fuera del archivo generado.
    public virtual DbSet<Ticket> Tickets { get; set; }

    public virtual DbSet<TicketComment> TicketComments { get; set; }

    public virtual DbSet<TicketCategory> TicketCategories { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TicketCategory>(entity =>
        {
            entity.HasKey(e => e.TicketCategoryId);
            entity.ToTable("ticket_categories", "inventario");

            entity.HasIndex(e => e.Code).IsUnique();

            entity.Property(e => e.TicketCategoryId).UseIdentityAlwaysColumn().HasColumnName("ticket_category_id");
            entity.Property(e => e.Code).HasMaxLength(40).HasColumnName("code");
            entity.Property(e => e.Name).HasMaxLength(120).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Version).HasDefaultValue(1).HasColumnName("version");
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.TicketId);
            entity.ToTable("tickets", "inventario");

            entity.Property(e => e.TicketId).UseIdentityAlwaysColumn().HasColumnName("ticket_id");
            entity.Property(e => e.RequesterId).HasColumnName("requester_id");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.TicketCategoryId).HasColumnName("ticket_category_id");
            entity.Property(e => e.IncidentPriorityId).HasColumnName("incident_priority_id");
            entity.Property(e => e.Status).HasMaxLength(20).HasColumnName("status");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Resolution).HasColumnName("resolution");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version).HasDefaultValue(1).HasColumnName("version");

            entity.HasOne(e => e.Requester).WithMany()
                .HasForeignKey(e => e.RequesterId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.AssignedToNavigation).WithMany()
                .HasForeignKey(e => e.AssignedTo)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.TicketCategory).WithMany()
                .HasForeignKey(e => e.TicketCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.IncidentPriority).WithMany()
                .HasForeignKey(e => e.IncidentPriorityId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.Asset).WithMany()
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.Location).WithMany()
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.Incident).WithMany()
                .HasForeignKey(e => e.IncidentId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TicketComment>(entity =>
        {
            entity.HasKey(e => e.TicketCommentId);
            entity.ToTable("ticket_comments", "inventario");

            entity.Property(e => e.TicketCommentId).UseIdentityAlwaysColumn().HasColumnName("ticket_comment_id");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.IsInternal).HasColumnName("is_internal");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(e => e.Ticket).WithMany(t => t.Comments)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(e => e.Author).WithMany()
                .HasForeignKey(e => e.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.RefreshTokenId);
            entity.ToTable("refresh_tokens", "inventario");

            entity.HasIndex(e => e.TokenHash).IsUnique();

            entity.Property(e => e.RefreshTokenId).UseIdentityAlwaysColumn().HasColumnName("refresh_token_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsFixedLength().HasColumnName("token_hash");
            entity.Property(e => e.FamilyId).HasColumnName("family_id");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.ReplacedById).HasColumnName("replaced_by_id");
            entity.Property(e => e.CreatedIp).HasColumnName("created_ip");
            entity.Property(e => e.UserAgent).HasMaxLength(512).HasColumnName("user_agent");

            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<UserExternalLogin>(entity =>
        {
            entity.HasKey(e => e.UserExternalLoginId);
            entity.ToTable("user_external_logins", "inventario");

            entity.HasIndex(e => new { e.Provider, e.Subject }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.Provider }).IsUnique();

            entity.Property(e => e.UserExternalLoginId).UseIdentityAlwaysColumn().HasColumnName("user_external_login_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Provider).HasMaxLength(20).HasColumnName("provider");
            entity.Property(e => e.Subject).HasMaxLength(255).HasColumnName("subject");
            entity.Property(e => e.Email).HasMaxLength(254).HasColumnName("email");
            entity.Property(e => e.LinkedAt).HasDefaultValueSql("now()").HasColumnName("linked_at");

            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var versionProp = entityType.FindProperty("Version");
            if (versionProp is not null && versionProp.ClrType == typeof(int))
                versionProp.IsConcurrencyToken = true;
        }
    }
}

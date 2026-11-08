using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Locations.Shared;

/// <summary>
/// Una ubicación y todas sus sub-ubicaciones (edificio -> plantas -> salones).
/// El árbol de un plantel es acotado: BFS en memoria sobre (id, padre) en
/// lugar de un CTE recursivo. Ubicación inexistente -> lista vacía.
/// </summary>
public static class LocationTree
{
    public static async Task<List<long>> DescendantsAndSelfAsync(
        TecnmInventarioDbContext context, long rootId, CancellationToken cancellationToken)
    {
        var all = await context.Locations.AsNoTracking()
            .Select(l => new { l.LocationId, l.ParentLocationId })
            .ToListAsync(cancellationToken);

        if (all.All(l => l.LocationId != rootId))
            return new List<long>();

        var childrenByParent = all
            .Where(l => l.ParentLocationId != null)
            .GroupBy(l => l.ParentLocationId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(l => l.LocationId).ToList());

        var result = new List<long> { rootId };
        var seen = new HashSet<long> { rootId };
        for (var i = 0; i < result.Count; i++)
        {
            if (!childrenByParent.TryGetValue(result[i], out var children)) continue;
            foreach (var child in children)
            {
                if (seen.Add(child)) result.Add(child);
            }
        }
        return result;
    }
}

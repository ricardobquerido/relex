using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Relex.Domain;

namespace Relex.Api.Infrastructure;

public interface ILookupCache
{
    short? GetLocationId(string code);
    int? GetProductId(string code);
}

/// <summary>
/// A high-performance, thread-safe cache for dimension lookups.
/// In high-scale systems, dimensions (Products, Locations) are relatively small and static
/// compared to the Fact table (Orders). Caching them eliminates millions of DB roundtrips.
/// </summary>
public class LookupCache : ILookupCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, short> _locations = new();
    private readonly ConcurrentDictionary<string, int> _products = new();
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LookupCache(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_isInitialized) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RelexDbContext>();

            // Load all locations
            var locs = await db.Locations.AsNoTracking().ToListAsync(ct);
            foreach (var l in locs) _locations[l.Code] = l.Id;

            // Load all products
            var prods = await db.Products.AsNoTracking().ToListAsync(ct);
            foreach (var p in prods) _products[p.Code] = p.Id;

            _isInitialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public short? GetLocationId(string code)
    {
        if (_locations.TryGetValue(code, out var id)) return id;
        return null;
    }

    public int? GetProductId(string code)
    {
        if (_products.TryGetValue(code, out var id)) return id;
        return null;
    }
}

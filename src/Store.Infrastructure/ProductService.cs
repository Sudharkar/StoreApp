using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Store.Application.DTOs;
using Store.Application.Exceptions;
using Store.Application.Interfaces;
using Store.Domain.Entities;
using Store.Infrastructure.Data;

namespace Store.Infrastructure.Services;

public class ProductService : IProductService
{
    private const string CacheVersionKey = "products:version";

    private readonly StoreDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProductService> _logger;

    public ProductService(StoreDbContext db, IMemoryCache cache, ILogger<ProductService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductQuery q, CancellationToken ct = default)
    {
        q.Page = Math.Max(1, q.Page);
        q.PageSize = Math.Clamp(q.PageSize, 1, 100);

        // cache key includes a version number; bumping the version invalidates all cached lists
        var version = _cache.GetOrCreate(CacheVersionKey, _ => 0);
        var cacheKey = $"products:v{version}:{q.Name}:{q.MinPrice}:{q.MaxPrice}:{q.InStock}:{q.SortBy}:{q.Desc}:{q.Page}:{q.PageSize}";

        if (_cache.TryGetValue(cacheKey, out PagedResult<ProductDto>? cached) && cached is not null)
            return cached;

        // everything below runs in SQL (IQueryable), nothing is filtered in memory
        IQueryable<Product> query = _db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(q.Name))
        {
            var name = q.Name.Trim();
            query = query.Where(p => p.Name.Contains(name) || p.Sku.Contains(name));
        }
        if (q.MinPrice.HasValue) query = query.Where(p => p.Price >= q.MinPrice.Value);
        if (q.MaxPrice.HasValue) query = query.Where(p => p.Price <= q.MaxPrice.Value);
        if (q.InStock == true) query = query.Where(p => p.StockQuantity > 0);

        query = (q.SortBy?.ToLowerInvariant(), q.Desc) switch
        {
            ("price", false) => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            ("price", true) => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            ("stock", false) => query.OrderBy(p => p.StockQuantity).ThenBy(p => p.Id),
            ("stock", true) => query.OrderByDescending(p => p.StockQuantity).ThenBy(p => p.Id),
            ("newest", _) => query.OrderByDescending(p => p.Id),
            (_, true) => query.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
            _ => query.OrderBy(p => p.Name).ThenBy(p => p.Id)
        };

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            })
            .ToListAsync(ct);

        var result = new PagedResult<ProductDto>
        {
            Items = items,
            Page = q.Page,
            PageSize = q.PageSize,
            TotalCount = total
        };

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
        return result;
    }

    public async Task<ProductDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var p = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Product {id} not found.");
        return ToDto(p);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest r, CancellationToken ct = default)
    {
        var sku = r.Sku.Trim().ToUpperInvariant();
        if (await _db.Products.AnyAsync(p => p.Sku == sku, ct))
            throw new ConflictException($"SKU '{sku}' already exists.");

        var product = new Product
        {
            Sku = sku,
            Name = r.Name.Trim(),
            Price = r.Price,
            StockQuantity = r.StockQuantity,
            IsActive = r.IsActive
        };
        _db.Products.Add(product);

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException($"SKU '{sku}' already exists."); }

        InvalidateCache();
        _logger.LogInformation("Product created: {ProductId} {Sku}", product.Id, product.Sku);
        return ToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductRequest r, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException($"Product {id} not found.");

        product.Name = r.Name.Trim();
        product.Price = r.Price;
        product.StockQuantity = r.StockQuantity;
        product.IsActive = r.IsActive;

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Product was modified by someone else. Please reload and try again.");
        }

        InvalidateCache();
        _logger.LogInformation("Product updated: {ProductId}", product.Id);
        return ToDto(product);
    }

    // bump version => all old cache keys become unreachable (they expire in 2 min)
    private void InvalidateCache()
    {
        var version = _cache.GetOrCreate(CacheVersionKey, _ => 0);
        _cache.Set(CacheVersionKey, version + 1);
    }

    private static ProductDto ToDto(Product p) => new()
    {
        Id = p.Id,
        Sku = p.Sku,
        Name = p.Name,
        Price = p.Price,
        StockQuantity = p.StockQuantity,
        IsActive = p.IsActive
    };
}
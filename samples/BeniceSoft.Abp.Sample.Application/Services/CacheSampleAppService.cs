using BeniceSoft.Abp.Extensions.Caching.Abstractions.Annotations;
using Volo.Abp.Application.Services;

namespace BeniceSoft.Abp.Sample.Application.Services;

/// <summary>
/// 缓存功能示例服务
/// </summary>
public class CacheSampleAppService : ApplicationService
{
    /// <summary>
    /// 基础缓存示例 - 使用默认缓存键（方法签名 + 参数类型）
    /// 缓存 60 秒
    /// </summary>
    [Cacheable(ExpirationSeconds = 60)]
    public virtual async Task<ProductDto> GetProductAsync(int productId)
    {
        // 模拟数据库查询
        await Task.Delay(100);
        
        return new ProductDto
        {
            Id = productId,
            Name = $"Product {productId}",
            Price = productId * 10.5m,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 自定义缓存键示例 - 使用表达式生成缓存键
    /// 缓存键格式: product:{productId}
    /// </summary>
    [Cacheable(Key = "\"product:\" + productId.ToString()", ExpirationSeconds = 120)]
    public virtual async Task<ProductDto> GetProductWithCustomKeyAsync(int productId)
    {
        await Task.Delay(100);
        
        return new ProductDto
        {
            Id = productId,
            Name = $"Product {productId}",
            Price = productId * 10.5m,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 条件缓存示例 - 只有当 productId > 0 时才缓存
    /// </summary>
    [Cacheable(Condition = "productId > 0", ExpirationSeconds = 60)]
    public virtual async Task<ProductDto?> GetProductWithConditionAsync(int productId)
    {
        await Task.Delay(100);
        
        if (productId <= 0)
        {
            return null;
        }

        return new ProductDto
        {
            Id = productId,
            Name = $"Product {productId}",
            Price = productId * 10.5m,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Unless 示例 - 当返回值为 null 时不缓存
    /// </summary>
    [Cacheable(Unless = "@result == null", ExpirationSeconds = 60)]
    public virtual async Task<ProductDto?> GetProductUnlessNullAsync(int productId)
    {
        await Task.Delay(100);
        
        // 模拟某些情况下返回 null
        if (productId % 2 == 0)
        {
            return null;
        }

        return new ProductDto
        {
            Id = productId,
            Name = $"Product {productId}",
            Price = productId * 10.5m,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 复杂参数缓存示例 - 使用对象属性生成缓存键
    /// </summary>
    [Cacheable(Key = "\"search:\" + query.Keyword + \":\" + query.Page.ToString()", ExpirationSeconds = 30)]
    public virtual async Task<ProductListDto> SearchProductsAsync(ProductSearchQuery query)
    {
        await Task.Delay(100);
        
        var products = Enumerable.Range(1, query.PageSize)
            .Select(i => new ProductDto
            {
                Id = (query.Page - 1) * query.PageSize + i,
                Name = $"{query.Keyword} Product {i}",
                Price = i * 5.0m,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        return new ProductListDto
        {
            Items = products,
            TotalCount = 100,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// 长时间缓存示例 - 缓存 30 分钟
    /// </summary>
    [Cacheable(Key = "\"categories\"", ExpirationSeconds = 1800)]
    public virtual async Task<List<CategoryDto>> GetAllCategoriesAsync()
    {
        await Task.Delay(200);
        
        return new List<CategoryDto>
        {
            new() { Id = 1, Name = "Electronics" },
            new() { Id = 2, Name = "Clothing" },
            new() { Id = 3, Name = "Books" },
            new() { Id = 4, Name = "Home & Garden" }
        };
    }
}

#region DTOs

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductSearchQuery
{
    public string Keyword { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ProductListDto
{
    public List<ProductDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

#endregion


namespace Catalog.Application;

using Microsoft.Extensions.DependencyInjection;
using Catalog.Application.Services.Category;
using Catalog.Application.Services.Product;
using Catalog.Application.Services.Report;
using Catalog.Application.Services.Review;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<Catalog.Application.Services.Audit.IAuditSyncSourceService, Catalog.Application.Services.Audit.AuditSyncSourceService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductInventoryInitializer, ProductInventoryInitializer>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductReportService, ProductReportService>();
        services.AddScoped<IProductReviewService, ProductReviewService>();
        return services;
    }
}

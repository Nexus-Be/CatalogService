namespace Catalog.Application;

using Microsoft.Extensions.DependencyInjection;
using Catalog.Application.Services.Category;
using Catalog.Application.Services.Product;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        return services;
    }
}

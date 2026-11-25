namespace Catalog.Tests;

using System;
using System.Linq;
using System.Reflection;
using Catalog.Api.Controllers.Public;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

/// <summary>
/// Verifies that all public controllers in Catalog.Api expose the exact route templates and HTTP verbs
/// expected by the Gateway YARP configuration (Gateway/appsettings.example.json and Gateway.Tests/RouteCatalogConfigurationTests.cs).
/// This prevents any accidental route regressions that would break API Gateway routing.
/// </summary>
public sealed class GatewayRouteContractConsistencyTests
{
    [Theory]
    [InlineData(typeof(ReviewsController), "api/v1/reviews")]
    [InlineData(typeof(AdminReviewsController), "api/v1/admin/reviews")]
    [InlineData(typeof(AdminProductsController), "api/v1/admin/products")]
    [InlineData(typeof(AdminProductReportsController), "api/v1/admin/reports/products")]
    [InlineData(typeof(ProductsController), "api/v1/products")]
    [InlineData(typeof(CategoriesController), "api/v1/categories")]
    [InlineData(typeof(VariantsController), "api/v1/variants")]
    [InlineData(typeof(ImagesController), "api/v1/images")]
    [InlineData(typeof(SellerProductsController), "api/v1/seller/products")]
    public void Controller_ExposesExpectedBaseRoute_ForGatewayReverseProxy(Type controllerType, string expectedRoute)
    {
        var routeAttr = controllerType.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal(expectedRoute, routeAttr.Template);
    }

    [Fact]
    public void ReviewsController_ExposesPutAndDelete_MatchingGatewayCatalogReviewByIdRoute()
    {
        var type = typeof(ReviewsController);

        // Gateway routes /api/v1/reviews/{id:guid} with PUT and DELETE to CatalogService
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        var putMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<HttpPutAttribute>()?.Template == "{id:guid}");
        Assert.NotNull(putMethod);

        var deleteMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<HttpDeleteAttribute>()?.Template == "{id:guid}");
        Assert.NotNull(deleteMethod);
    }

    [Fact]
    public void AdminReviewsController_ExposesPatchModerate_MatchingGatewayAdminProductReviewsModerateRoute()
    {
        var type = typeof(AdminReviewsController);

        // Gateway routes /api/v1/admin/reviews/{id}/moderate to CatalogService
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        var patchModerateMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<HttpPatchAttribute>()?.Template == "{id:guid}/moderate");
        Assert.NotNull(patchModerateMethod);
    }

    [Fact]
    public void AdminProductReportsController_ExposesReportsProductsRoute_MatchingGatewayAdminProductReportsRoute()
    {
        var type = typeof(AdminProductReportsController);

        var routeAttr = type.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/v1/admin/reports/products", routeAttr.Template);
    }
}

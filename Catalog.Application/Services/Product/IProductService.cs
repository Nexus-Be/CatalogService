namespace Catalog.Application.Services.Product;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Application.Common.Models;

public interface IProductService
{
    Task<ProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedData<ProductDto>> ListAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
}

namespace Catalog.Application.Services.Product;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> ListAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}

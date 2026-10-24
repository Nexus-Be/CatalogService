namespace Catalog.Application.Services.Category;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ICategoryService
{
    Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryDto>> ListAllAsync(CancellationToken cancellationToken = default);
}

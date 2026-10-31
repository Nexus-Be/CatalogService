namespace Catalog.Application.Services.Category;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Application.Common.Models;

public interface ICategoryService
{
    Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedData<CategoryDto>> ListAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

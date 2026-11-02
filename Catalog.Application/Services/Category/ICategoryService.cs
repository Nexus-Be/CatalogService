namespace Catalog.Application.Services.Category;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Application.Common.Models;

public interface ICategoryService
{
    Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedData<CategoryDto>> ListAllAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<PagedData<CategoryItemDto>> GetCategoriesAsync(CategoryListQuery query, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, Guid? currentUserId = null, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, Guid? currentUserId = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid? currentUserId = null, CancellationToken cancellationToken = default);
}

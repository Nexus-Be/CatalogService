namespace Catalog.Application.Interfaces;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

public sealed record CloudImageUploadResult(
    string Url,
    string PublicId,
    long Bytes,
    string Format);

public interface IImageStorageService
{
    Task<CloudImageUploadResult> UploadAsync(
        Stream stream, 
        string fileName, 
        string folder = "products", 
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string publicId, 
        CancellationToken cancellationToken = default);
}


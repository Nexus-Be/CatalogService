namespace Catalog.Application.Common.Models;

using System.IO;

public sealed record FileUploadModel(Stream Stream, string FileName);


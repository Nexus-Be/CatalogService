namespace Catalog.Application.Services.Product;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Catalog.Domain.Exceptions;

public static class ProductSpecificationHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string? ValidateAndSerialize(List<ProductSpecificationDto>? specs)
    {
        if (specs == null || specs.Count == 0)
        {
            return null;
        }

        if (specs.Count > 30)
        {
            throw new DomainException(
                "VAL_LIMIT_EXCEEDED",
                "A product can have a maximum of 30 specifications.",
                400);
        }

        var cleanedList = new List<ProductSpecificationDto>(specs.Count);

        foreach (var spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec.Name))
            {
                throw new DomainException(
                    "VAL_INVALID_INPUT",
                    "Specification name cannot be empty.",
                    400);
            }

            var trimmedName = spec.Name.Trim();
            if (trimmedName.Length > 100)
            {
                throw new DomainException(
                    "VAL_INVALID_INPUT",
                    $"Specification name '{trimmedName}' exceeds maximum length of 100 characters.",
                    400);
            }

            var trimmedValue = spec.Value?.Trim() ?? string.Empty;
            if (trimmedValue.Length > 500)
            {
                throw new DomainException(
                    "VAL_INVALID_INPUT",
                    $"Specification value for '{trimmedName}' exceeds maximum length of 500 characters.",
                    400);
            }

            cleanedList.Add(new ProductSpecificationDto(trimmedName, trimmedValue));
        }

        return JsonSerializer.Serialize(cleanedList, JsonOptions);
    }

    public static IReadOnlyList<ProductSpecificationDto> Deserialize(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return Array.Empty<ProductSpecificationDto>();
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            // Case 1: New JSON array format [{"name": "...", "value": "..."}]
            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = new List<ProductSpecificationDto>();
                foreach (var element in root.EnumerateArray())
                {
                    string name = string.Empty;
                    string val = string.Empty;

                    if (element.TryGetProperty("name", out var nameProp) || element.TryGetProperty("Name", out nameProp))
                    {
                        name = nameProp.GetString() ?? string.Empty;
                    }
                    else if (element.TryGetProperty("key", out var keyProp) || element.TryGetProperty("Key", out keyProp))
                    {
                        name = keyProp.GetString() ?? string.Empty;
                    }

                    if (element.TryGetProperty("value", out var valProp) || element.TryGetProperty("Value", out valProp))
                    {
                        val = valProp.GetString() ?? valProp.ToString();
                    }

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        list.Add(new ProductSpecificationDto(name, val));
                    }
                }

                return list;
            }

            // Case 2: Legacy JSON object format {"ram": "12GB", "storage": "256GB"}
            if (root.ValueKind == JsonValueKind.Object)
            {
                var list = new List<ProductSpecificationDto>();
                foreach (var prop in root.EnumerateObject())
                {
                    list.Add(new ProductSpecificationDto(prop.Name, prop.Value.ToString()));
                }

                return list;
            }
        }
        catch
        {
            // Defensive: if DB contains invalid or corrupt JSON string, return empty list instead of crashing
        }

        return Array.Empty<ProductSpecificationDto>();
    }
}


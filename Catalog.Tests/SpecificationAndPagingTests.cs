namespace Catalog.Tests;

using Catalog.Application.Common;
using Catalog.Application.Services.Product;
using Catalog.Domain.Exceptions;
using Xunit;

/// <summary>Product specifications are stored as JSON text and read back from rows written by older versions, so both directions are pinned.</summary>
public sealed class ProductSpecificationHelperTests
{
    [Fact]
    public void No_specifications_store_nothing()
    {
        Assert.Null(ProductSpecificationHelper.ValidateAndSerialize(null));
        Assert.Null(ProductSpecificationHelper.ValidateAndSerialize(new List<ProductSpecificationDto>()));
    }

    [Fact]
    public void Names_and_values_are_trimmed_and_a_missing_value_becomes_empty()
    {
        var json = ProductSpecificationHelper.ValidateAndSerialize(new List<ProductSpecificationDto>
        {
            new("  RAM ", " 12GB  "), new("Color", null!)
        });

        var back = ProductSpecificationHelper.Deserialize(json);

        Assert.Equal(new[] { new ProductSpecificationDto("RAM", "12GB"), new ProductSpecificationDto("Color", "") }, back);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_name_is_refused(string name)
    {
        var error = Assert.Throws<DomainException>(() =>
            ProductSpecificationHelper.ValidateAndSerialize(new List<ProductSpecificationDto> { new(name, "x") }));

        Assert.Equal("VAL_INVALID_INPUT", error.ErrorCode);
    }

    [Fact]
    public void The_limits_are_30_specifications_100_characters_for_a_name_and_500_for_a_value()
    {
        var thirty = Enumerable.Range(0, 30).Select(i => new ProductSpecificationDto("n" + i, "v")).ToList();
        var thirtyOne = thirty.Append(new ProductSpecificationDto("extra", "v")).ToList();

        Assert.NotNull(ProductSpecificationHelper.ValidateAndSerialize(thirty));
        Assert.Equal("VAL_LIMIT_EXCEEDED", Assert.Throws<DomainException>(() => ProductSpecificationHelper.ValidateAndSerialize(thirtyOne)).ErrorCode);

        Assert.NotNull(ProductSpecificationHelper.ValidateAndSerialize(new List<ProductSpecificationDto> { new(new string('n', 100), new string('v', 500)) }));
        Assert.Throws<DomainException>(() => ProductSpecificationHelper.ValidateAndSerialize(new List<ProductSpecificationDto> { new(new string('n', 101), "v") }));
        Assert.Throws<DomainException>(() => ProductSpecificationHelper.ValidateAndSerialize(new List<ProductSpecificationDto> { new("n", new string('v', 501)) }));
    }

    [Fact]
    public void The_current_array_format_is_read_with_either_casing()
    {
        var read = ProductSpecificationHelper.Deserialize("""[{"name":"RAM","value":"12GB"},{"Name":"Storage","Value":"256GB"}]""");

        Assert.Equal(new[] { new ProductSpecificationDto("RAM", "12GB"), new ProductSpecificationDto("Storage", "256GB") }, read);
    }

    [Fact]
    public void The_older_key_value_array_and_the_oldest_object_format_are_still_read()
    {
        var keyed = ProductSpecificationHelper.Deserialize("""[{"key":"Brand","value":"Acme"}]""");
        var legacy = ProductSpecificationHelper.Deserialize("""{"ram":"12GB","storage":"256GB","cores":8}""");

        Assert.Equal(new[] { new ProductSpecificationDto("Brand", "Acme") }, keyed);
        Assert.Equal(new[]
        {
            new ProductSpecificationDto("ram", "12GB"), new ProductSpecificationDto("storage", "256GB"), new ProductSpecificationDto("cores", "8")
        }, legacy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("42")]
    [InlineData("\"text\"")]
    public void Empty_or_corrupt_stored_text_gives_an_empty_list_never_an_error(string? raw)
    {
        Assert.Empty(ProductSpecificationHelper.Deserialize(raw));
    }

    [Fact]
    public void Entries_without_a_name_are_dropped_when_reading()
    {
        var read = ProductSpecificationHelper.Deserialize("""[{"name":"","value":"x"},{"value":"y"},{"name":"Kept","value":"z"}]""");

        Assert.Equal(new[] { new ProductSpecificationDto("Kept", "z") }, read);
    }
}

public sealed class PaginationConstantsTests
{
    [Theory]
    [InlineData(1, 20, 1, 20)]
    [InlineData(0, 20, 1, 20)]       // a page below 1 starts from the first page
    [InlineData(-4, 20, 1, 20)]
    [InlineData(3, 0, 3, 20)]        // a size below 1 falls back to the default
    [InlineData(3, -1, 3, 20)]
    [InlineData(3, 100, 3, 100)]     // the maximum is allowed
    [InlineData(3, 101, 3, 100)]     // above it is cut to the maximum
    [InlineData(3, 5000, 3, 100)]
    [InlineData(7, 1, 7, 1)]
    public void Page_and_size_are_always_safe_to_use_in_a_query(int page, int size, int expectedPage, int expectedSize)
    {
        Assert.Equal((expectedPage, expectedSize), PaginationConstants.Normalize(page, size));
    }
}

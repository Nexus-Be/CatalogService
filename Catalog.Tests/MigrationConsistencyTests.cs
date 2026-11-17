namespace Catalog.Tests;

using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

/// <summary>
/// Migrations here are written by hand (dotnet-ef is not installed), so these checks catch a snapshot that drifts from the model
/// or a migration that cannot produce SQL. No database connection is opened.
/// </summary>
public sealed class MigrationConsistencyTests
{
    [Fact]
    public void Snapshot_MatchesCurrentModel()
    {
        using var db = CreateNpgsqlContext();
        var differ = db.GetService<IMigrationsModelDiffer>();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot
                       ?? throw new InvalidOperationException("Model snapshot was not found.");
        var initializer = db.GetService<IModelRuntimeInitializer>();
        var snapshotModel = initializer.Initialize(snapshot.Model, designTime: true, validationLogger: null);
        var designModel = db.GetService<IDesignTimeModel>().Model;

        var differences = differ.GetDifferences(snapshotModel.GetRelationalModel(), designModel.GetRelationalModel());

        var described = differences.Select(Describe).ToList();
        Assert.True(described.Count == 0, "Snapshot differs from the model: " + string.Join(" | ", described));
    }

    [Fact]
    public void VariantInventoryInitStateMigration_GeneratesSql()
    {
        using var db = CreateNpgsqlContext();
        var script = db.GetService<IMigrator>().GenerateScript(
            fromMigration: "20261112052732_AddReviewImagesToProductReviews",
            toMigration: "20261116130000_AddVariantInventoryInitState");

        Assert.Contains("InventoryInitState", script);
    }

    private static string Describe(MigrationOperation op) => op switch
    {
        AddColumnOperation a => $"AddColumn {a.Table}.{a.Name}",
        DropColumnOperation d => $"DropColumn {d.Table}.{d.Name}",
        AlterColumnOperation c => $"AlterColumn {c.Table}.{c.Name}",
        CreateIndexOperation i => $"CreateIndex {i.Table}.{i.Name}",
        DropIndexOperation i => $"DropIndex {i.Table}.{i.Name}",
        CreateTableOperation t => $"CreateTable {t.Name}",
        _ => op.GetType().Name
    };

    private static AppDbContext CreateNpgsqlContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options);
}

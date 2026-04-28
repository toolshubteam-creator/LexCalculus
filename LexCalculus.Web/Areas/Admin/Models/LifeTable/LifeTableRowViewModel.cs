namespace LexCalculus.Web.Areas.Admin.Models.LifeTable;

public sealed class LifeTableRowViewModel
{
    public int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public DateTime EffectiveDate { get; init; }
    public bool IsActive { get; init; }
    public int RowCount { get; init; }
    public string? Source { get; init; }
}

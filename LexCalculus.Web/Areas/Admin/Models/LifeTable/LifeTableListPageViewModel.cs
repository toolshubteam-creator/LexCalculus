namespace LexCalculus.Web.Areas.Admin.Models.LifeTable;

public sealed class LifeTableListPageViewModel
{
    public required IReadOnlyList<LifeTableRowViewModel> Items { get; init; }
    public LifeTableRowViewModel? ActiveTable
        => Items.FirstOrDefault(t => t.IsActive);
}

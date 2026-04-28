using LexCalculus.Core.Enums;

namespace LexCalculus.Web.Areas.Admin.Models.LifeTable;

public sealed class LifeTableDetailRowViewModel
{
    public int Id { get; init; }
    public int Yas { get; init; }
    public Cinsiyet Cinsiyet { get; init; }
    public decimal BekledigiYasam { get; init; }
}

using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;

namespace LexCalculus.Web.Areas.Admin.Models.LifeTable;

public sealed class LifeTableRowEditViewModel
{
    public int TableId { get; set; }
    public int RowId { get; set; }
    public string TableCode { get; set; } = "";
    public int Yas { get; set; }
    public Cinsiyet Cinsiyet { get; set; }

    [Required(ErrorMessage = "Beklenen yaşam zorunlu.")]
    [Range(0.01, 200, ErrorMessage = "Pozitif sayı olmalı (0.01-200 aralığında).")]
    public decimal BekledigiYasam { get; set; }
}

using LexCalculus.Core.Entities.Moderation;

namespace LexCalculus.Web.Areas.Admin.Models.ContentReports;

public sealed class ContentReportDetailVm
{
    public ContentReportTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public string AuthorDisplayName { get; set; } = "";
    public string AuthorSlug { get; set; } = "";
    public string TargetTitle { get; set; } = "";
    public string TargetBodyHtml { get; set; } = "";
    public DateTime TargetCreatedAt { get; set; }
    public string? TargetPublicUrl { get; set; }
    public List<ContentReportListReport> Reports { get; set; } = new();
}

public sealed class ContentReportListReport
{
    public int Id { get; set; }
    public string ReporterDisplayName { get; set; } = "";
    public ContentReportReason Reason { get; set; }
    public string ReasonLabel { get; set; } = "";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

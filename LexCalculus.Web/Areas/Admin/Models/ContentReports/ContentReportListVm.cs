using LexCalculus.Core.Entities.Moderation;

namespace LexCalculus.Web.Areas.Admin.Models.ContentReports;

public sealed class ContentReportListVm
{
    public List<ContentReportListItem> Items { get; set; } = new();
    public int TotalPending { get; set; }
}

public sealed class ContentReportListItem
{
    public ContentReportTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public int ReportCount { get; set; }
    public DateTime LatestReportAt { get; set; }
    public string TargetTitle { get; set; } = "";
    public string AuthorDisplayName { get; set; } = "";
    public string? TargetUrl { get; set; }
}

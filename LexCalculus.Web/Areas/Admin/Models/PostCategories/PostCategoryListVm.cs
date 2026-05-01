using LexCalculus.Core.Entities.Content;

namespace LexCalculus.Web.Areas.Admin.Models.PostCategories;

public sealed class PostCategoryListVm
{
    public IReadOnlyList<PostCategory> Items { get; set; } = Array.Empty<PostCategory>();
}

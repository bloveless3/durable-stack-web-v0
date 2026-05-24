using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DurableStack.App.Extensions;

public static class HtmlHelperExtensions
{
    private const string TitlePartsKey = "DurableStack.TitleParts";
    private const string AppName = "DurableStack";

    public static void AddTitleParts(this IHtmlHelper html, params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(html);

        var titleParts = GetOrCreateTitleParts(html);

        if (parts is null)
        {
            return;
        }

        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                titleParts.Add(part.Trim());
            }
        }
    }

    public static IHtmlContent RenderTitleParts(this IHtmlHelper html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var titleParts = GetOrCreateTitleParts(html);
        var fullTitle = titleParts.Count == 0
            ? AppName
            : $"{string.Join(" - ", titleParts)} - {AppName}";

        var titleTag = new TagBuilder("title");
        titleTag.InnerHtml.Append(fullTitle);

        using var writer = new StringWriter();
        titleTag.WriteTo(writer, HtmlEncoder.Default);

        return new HtmlString(writer.ToString());
    }

    public static string GetCurrentPageTitle(this IHtmlHelper html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var titleParts = GetOrCreateTitleParts(html);
        return titleParts.Count == 0 ? AppName : titleParts[0];
    }

    private static IList<string> GetOrCreateTitleParts(IHtmlHelper html)
    {
        var items = html.ViewContext.HttpContext.Items;

        if (items.TryGetValue(TitlePartsKey, out var existingParts) && existingParts is IList<string> parsedParts)
        {
            return parsedParts;
        }

        var titleParts = new List<string>();
        items[TitlePartsKey] = titleParts;

        return titleParts;
    }
}

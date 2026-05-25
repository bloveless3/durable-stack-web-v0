using System.Text.Encodings.Web;
using DurableStack.App.Menu;
using DurableStack.App.Models.Layout;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DurableStack.App.Extensions;

public static class HtmlHelperExtensions
{
    private const string TitlePartsKey = "DurableStack.TitleParts";
    private const string ActiveMenuKey = "DurableStack.ActiveMenuKey";
    private const string BreadcrumbPartsKey = "DurableStack.BreadcrumbParts";
    private const string ShowGlobalFiltersKey = "DurableStack.ShowGlobalFilters";
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

    public static void SetActiveMenuItem(this IHtmlHelper html, string menuKey)
    {
        ArgumentNullException.ThrowIfNull(html);

        if (string.IsNullOrWhiteSpace(menuKey))
        {
            return;
        }

        html.ViewContext.HttpContext.Items[ActiveMenuKey] = menuKey.Trim();
    }

    public static string? GetActiveMenuItem(this IHtmlHelper html)
    {
        ArgumentNullException.ThrowIfNull(html);

        return html.ViewContext.HttpContext.Items.TryGetValue(ActiveMenuKey, out var value)
            ? value as string
            : null;
    }

    public static AppMenuViewModel BuildAppMenu(this IHtmlHelper html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var provider = html.ViewContext.HttpContext.RequestServices.GetService(typeof(IAppMenuProvider)) as IAppMenuProvider;
        var items = provider?.GetMenu(html.ViewContext.HttpContext.User) ?? Array.Empty<AppMenuItem>();

        return new AppMenuViewModel
        {
            Items = items,
            ActiveKey = html.GetActiveMenuItem()
        };
    }

    public static void AddBreadcrumbsPart(this IHtmlHelper html, string title, string? url = null)
    {
        ArgumentNullException.ThrowIfNull(html);

        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var parts = GetOrCreateBreadcrumbParts(html);
        parts.Add(new AppBreadcrumbPart
        {
            Title = title.Trim(),
            Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim()
        });
    }

    public static IReadOnlyList<AppBreadcrumbPart> GetBreadcrumbsParts(this IHtmlHelper html)
    {
        ArgumentNullException.ThrowIfNull(html);

        return GetOrCreateBreadcrumbParts(html).ToList();
    }

    public static void SetShowGlobalFilters(this IHtmlHelper html, bool show)
    {
        ArgumentNullException.ThrowIfNull(html);
        html.ViewContext.HttpContext.Items[ShowGlobalFiltersKey] = show;
    }

    public static bool GetShowGlobalFilters(this IHtmlHelper html)
    {
        ArgumentNullException.ThrowIfNull(html);

        if (html.ViewContext.HttpContext.Items.TryGetValue(ShowGlobalFiltersKey, out var value) && value is bool parsed)
        {
            return parsed;
        }

        return false;
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

    private static IList<AppBreadcrumbPart> GetOrCreateBreadcrumbParts(IHtmlHelper html)
    {
        var items = html.ViewContext.HttpContext.Items;

        if (items.TryGetValue(BreadcrumbPartsKey, out var existingParts) && existingParts is IList<AppBreadcrumbPart> parsedParts)
        {
            return parsedParts;
        }

        var breadcrumbParts = new List<AppBreadcrumbPart>();
        items[BreadcrumbPartsKey] = breadcrumbParts;

        return breadcrumbParts;
    }
}

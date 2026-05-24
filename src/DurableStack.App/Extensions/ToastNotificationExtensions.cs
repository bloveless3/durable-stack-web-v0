using System.Text.Json;
using DurableStack.App.Models.Layout;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DurableStack.App.Extensions;

public static class ToastNotificationExtensions
{
    private const string TempDataKey = "DurableStack.ToastNotifications";

    public static void AddToastNotification(this ITempDataDictionary tempData, string type, string message, int timeoutMs = 0)
    {
        ArgumentNullException.ThrowIfNull(tempData);

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var notifications = GetMutableToastNotifications(tempData);
        notifications.Add(new AppToastNotification
        {
            Type = NormalizeType(type),
            Message = message.Trim(),
            TimeoutMs = timeoutMs > 0 ? timeoutMs : 0
        });

        tempData[TempDataKey] = JsonSerializer.Serialize(notifications);
    }

    public static void AddSuccessToast(this ITempDataDictionary tempData, string message, int timeoutMs = 0)
    {
        AddToastNotification(tempData, "success", message, timeoutMs);
    }

    public static void AddErrorToast(this ITempDataDictionary tempData, string message, int timeoutMs = 0)
    {
        AddToastNotification(tempData, "error", message, timeoutMs);
    }

    public static void AddWarningToast(this ITempDataDictionary tempData, string message, int timeoutMs = 0)
    {
        AddToastNotification(tempData, "warning", message, timeoutMs);
    }

    public static void AddInfoToast(this ITempDataDictionary tempData, string message, int timeoutMs = 0)
    {
        AddToastNotification(tempData, "info", message, timeoutMs);
    }

    public static IReadOnlyList<AppToastNotification> GetToastNotifications(this ITempDataDictionary tempData)
    {
        ArgumentNullException.ThrowIfNull(tempData);

        if (!tempData.TryGetValue(TempDataKey, out var rawValue) || rawValue is null)
        {
            return Array.Empty<AppToastNotification>();
        }

        var json = rawValue.ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<AppToastNotification>();
        }

        try
        {
            var notifications = JsonSerializer.Deserialize<List<AppToastNotification>>(json);
            return notifications ?? new List<AppToastNotification>();
        }
        catch
        {
            return Array.Empty<AppToastNotification>();
        }
    }

    private static List<AppToastNotification> GetMutableToastNotifications(ITempDataDictionary tempData)
    {
        var existing = GetToastNotifications(tempData);
        return existing is List<AppToastNotification> parsedList ? parsedList : existing.ToList();
    }

    private static string NormalizeType(string type)
    {
        return type?.Trim().ToLowerInvariant() switch
        {
            "success" => "success",
            "error" => "error",
            "warning" => "warning",
            _ => "info"
        };
    }
}

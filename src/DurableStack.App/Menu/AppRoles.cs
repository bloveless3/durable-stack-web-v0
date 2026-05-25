namespace DurableStack.App.Menu;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = [Admin, Operator, Viewer];
}

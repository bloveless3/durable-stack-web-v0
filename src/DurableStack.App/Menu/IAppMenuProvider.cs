namespace DurableStack.App.Menu;

public interface IAppMenuProvider
{
    IReadOnlyList<AppMenuItem> GetMenu(System.Security.Claims.ClaimsPrincipal user);

    AppMenuItem? FindByKey(string key);
}

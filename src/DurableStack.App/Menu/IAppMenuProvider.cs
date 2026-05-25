namespace DurableStack.App.Menu;

public interface IAppMenuProvider
{
    IReadOnlyList<AppMenuItem> GetMenu();

    AppMenuItem? FindByKey(string key);
}

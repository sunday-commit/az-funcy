namespace Funcy.Console.Ui;

public interface IDetailsLoader
{
    void LoadDetails(string key);
    Task LoadAllDetailsAsync();
    bool CanRefreshAll();
    Task TogglePinAsync(string key);
}

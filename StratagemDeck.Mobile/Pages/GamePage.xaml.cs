using StratagemDeck.Mobile.ViewModels;

namespace StratagemDeck.Mobile.Pages;

public partial class GamePage : ContentPage
{
    private readonly GameViewModel _vm;

    public GamePage()
    {
        InitializeComponent();
        BindingContext = _vm = App.GetService<GameViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.InitializeAsync();
            _vm.UpdateConnectionStatus();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }
}

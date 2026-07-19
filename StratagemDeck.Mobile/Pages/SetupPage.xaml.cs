using StratagemDeck.Mobile.ViewModels;

namespace StratagemDeck.Mobile.Pages;

public partial class SetupPage : ContentPage
{
    private readonly SetupViewModel _vm;

    public SetupPage()
    {
        InitializeComponent();
        BindingContext = _vm = App.GetService<SetupViewModel>();
        _vm.SearchCompleted += OnSearchCompleted;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.InitializeAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private void OnSearchCompleted()
    {
        DismissKeyboard();
        if (_vm.AvailableStrats.Count > 0)
            StratsGrid.ScrollTo(0, position: Microsoft.Maui.Controls.ScrollToPosition.Start, animate: true);
    }

    private void DismissKeyboard()
    {
#if ANDROID
        if (StrSearchBar?.Handler?.PlatformView is Android.Views.View view)
        {
            var imm = view.Context?.GetSystemService(Android.Content.Context.InputMethodService)
                as Android.Views.InputMethods.InputMethodManager;
            imm?.HideSoftInputFromWindow(view.WindowToken, 0);
        }
#endif
    }
}

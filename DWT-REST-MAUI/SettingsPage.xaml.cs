using DWT_REST_MAUI.ViewModels;

namespace DWT_REST_MAUI;

public partial class SettingsPage : ContentPage
{
	private SettingsViewModel viewModel = new SettingsViewModel();
	public SettingsPage()
	{
		InitializeComponent();
		BindingContext = viewModel;
		LoadSettings();
    }

	private void LoadSettings() {
		viewModel.LoadPreferences();
		viewModel.LoadScanners();
	}
}
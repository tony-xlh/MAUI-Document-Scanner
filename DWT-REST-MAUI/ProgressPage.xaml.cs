namespace DWT_REST_MAUI;

public partial class ProgressPage : ContentPage
{
    private Func<object>? _callback;
	public ProgressPage()
	{
		InitializeComponent();
	}

    public void RegisterCallback(Func<object> callback) { 
        _callback = callback;
    }

    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        _callback?.Invoke();
    }
}
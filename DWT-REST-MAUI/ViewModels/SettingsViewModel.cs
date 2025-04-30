using DynamicWebTWAIN.RestClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DWT_REST_MAUI.ViewModels;

internal class SettingsViewModel : INotifyPropertyChanged
{
    // License Key
    private string _licenseKey;
    public string LicenseKey
    {
        get => _licenseKey;
        set
        {
            if (_licenseKey != value)
            {
                _licenseKey = value;
                OnPropertyChanged();
            }
        }
    }

    // IP Address
    private string _ipAddress;
    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (_ipAddress != value)
            {
                _ipAddress = value;
                OnPropertyChanged();
            }
        }
    }

    // Scanner Models
    private List<string> _scannerModels = new List<string>{};
    public List<string> ScannerModels
    {
        get => _scannerModels;
        set
        {
            _scannerModels = value;
            OnPropertyChanged();
        }
    }
    private string _selectedScannerModel;
    public string SelectedScannerModel
    {
        get => _selectedScannerModel;
        set
        {
            if (_selectedScannerModel != value)
            {
                _selectedScannerModel = value;
                OnPropertyChanged();
            }
        }
    }

    // DPI Options
    private bool _is150Dpi;
    public bool Is150Dpi
    {
        get => _is150Dpi;
        set
        {
            if (_is150Dpi != value)
            {
                _is150Dpi = value;
                OnPropertyChanged();
                if (value) SelectedDpi = 150;
            }
        }
    }

    private bool _is300Dpi = true; // Default to 300 DPI
    public bool Is300Dpi
    {
        get => _is300Dpi;
        set
        {
            if (_is300Dpi != value)
            {
                _is300Dpi = value;
                OnPropertyChanged();
                if (value) SelectedDpi = 300;
            }
        }
    }

    private bool _is600Dpi;
    public bool Is600Dpi
    {
        get => _is600Dpi;
        set
        {
            if (_is600Dpi != value)
            {
                _is600Dpi = value;
                OnPropertyChanged();
                if (value) SelectedDpi = 600;
            }
        }
    }

    public int SelectedDpi { get; private set; } = 300;

    // Color Mode Options
    private bool _isBlackWhite = true; // Default to B&W
    public bool IsBlackWhite
    {
        get => _isBlackWhite;
        set
        {
            if (_isBlackWhite != value)
            {
                _isBlackWhite = value;
                OnPropertyChanged();
                if (value) SelectedColorMode = "BlackWhite";
            }
        }
    }

    private bool _isGrayscale;
    public bool IsGrayscale
    {
        get => _isGrayscale;
        set
        {
            if (_isGrayscale != value)
            {
                _isGrayscale = value;
                OnPropertyChanged();
                if (value) SelectedColorMode = "Grayscale";
            }
        }
    }

    private bool _isColor;
    public bool IsColor
    {
        get => _isColor;
        set
        {
            if (_isColor != value)
            {
                _isColor = value;
                OnPropertyChanged();
                if (value) SelectedColorMode = "Color";
            }
        }
    }

    public string SelectedColorMode { get; private set; } = "BlackWhite";

    // Save Command
    public ICommand SaveSettingsCommand { get; }
    public ICommand LoadScannersCommand { get; }

    public SettingsViewModel()
    {
        SaveSettingsCommand = new Command(ExecuteSaveSettings);
        LoadScannersCommand = new Command(LoadScanners);
    }

    public void LoadPreferences() {
        LicenseKey = Preferences.Get("License", "");
        IpAddress = Preferences.Get("IP", "https://192.168.8.65:18623");
        int DPI = Preferences.Get("DPI",150);
        if (DPI == 150) {
            Is150Dpi = true;
        }
        else if (DPI == 300)
        {
            Is300Dpi = true;
        }
        else if (DPI == 600)
        {
            Is600Dpi = true;
        }
        SelectedColorMode = Preferences.Get("ColorMode", "Color");
        if (SelectedColorMode == "Blackwhite")
        {
            IsBlackWhite = true;
        }
        else if (SelectedColorMode == "Grayscale")
        {
            IsGrayscale = true;
        }
        else if (SelectedColorMode == "Color") {
            IsColor = true;
        }
    }

    public async void LoadScanners() {
        try
        {
            List<string> modelNames = new List<string>(); 
            var client = new DWTClient(new Uri(IpAddress), LicenseKey);
            var scanners = await client.ScannerControlClient.Manager.Get(EnumDeviceTypeMask.DT_TWAINSCANNER);
            foreach (var scanner in scanners)
            {
                Debug.WriteLine(scanner.Name);
                modelNames.Add(scanner.Name);
            }
            ScannerModels = modelNames;
            SelectedScannerModel = Preferences.Get("Scanner", "");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }

    }

    private void ExecuteSaveSettings()
    {
        Debug.WriteLine($"Saved");
        // Here you would implement your save logic
        // For example, save to preferences or send to a service
        Debug.WriteLine($"Settings Saved:\n" +
                            $"License: {LicenseKey}\n" +
                            $"IP: {IpAddress}\n" +
                            $"Scanner: {SelectedScannerModel}\n" +
                            $"DPI: {SelectedDpi}\n" +
                            $"Color Mode: {SelectedColorMode}");
        Preferences.Set("License", LicenseKey);
        Preferences.Set("IP", IpAddress);
        Preferences.Set("Scanner", SelectedScannerModel);
        Preferences.Set("DPI", SelectedDpi);
        Preferences.Set("ColorMode", SelectedColorMode);
        Shell.Current.GoToAsync("../");
        // In a real app, you might want to:
        // Preferences.Set("LicenseKey", LicenseKey);
        // Preferences.Set("IpAddress", IpAddress);
        // etc...
    }

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

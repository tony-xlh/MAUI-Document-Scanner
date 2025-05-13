using DynamicWebTWAIN.RestClient;
using Dynamsoft.WebViewer;
using Microsoft.Maui.Controls;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DWT_REST_MAUI
{
    public class HybridWebViewBridge : IWebViewBridge
    {
        private Microsoft.Maui.Controls.HybridWebView _webView;

        public HybridWebViewBridge(Microsoft.Maui.Controls.HybridWebView webView)
        {
            _webView = webView;

        }

        public async Task<string> ExecuteJavaScriptAsync(string script)
        {
            string result = null;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                result = await _webView.EvaluateJavaScriptAsync(script);
            });
            return result;
        }

        public void RegisterCallback(Func<string, bool> callback)
        {
            _webView.RawMessageReceived += (sender, args) =>
            {
                callback?.Invoke(args.Message);
            };
        }

        public async Task LoadUrlAsync(Uri url)
        {
            var tcs = new TaskCompletionSource();

            _webView.DefaultFile = url.OriginalString;

            await Task.CompletedTask;
        }
    }

    public partial class MainPage : ContentPage
    {
        private Dynamsoft.WebViewer.DocumentViewer _documentViewer;
        private IScannerJobClient? scannerJob;
        private string productKey = "DLS2eyJoYW5kc2hha2VDb2RlIjoiMTAwMjI3NzYzLVRYbFFjbTlxIiwibWFpblNlcnZlclVSTCI6Imh0dHBzOi8vbWx0cy5keW5hbXNvZnQuY29tIiwib3JnYW5pemF0aW9uSUQiOiIxMDAyMjc3NjMiLCJzdGFuZGJ5U2VydmVyVVJMIjoiaHR0cHM6Ly9zbHRzLmR5bmFtc29mdC5jb20iLCJjaGVja0NvZGUiOjE4OTc4MDUzNDV9";
        public MainPage()
        {
            InitializeComponent();
            webView.SetInvokeJavaScriptTarget(this);
            InitViewer();
            RequestCameraPermission();
        }

        private async void RequestCameraPermission() {
            PermissionStatus status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        private async void InitViewer() {
            DocumentViewerOptions options = new DocumentViewerOptions();
            // because we load ddv page in the service, so we should make sure the service is running, so we need to set a long timeout
            // or manual create a websocket connection in js, recommend this way.
            options.ProductKey = productKey;
            options.SiteUrl = "index.html";
            options.MessageType = "__RawMessage";
            var bridge = new HybridWebViewBridge(webView);
            _documentViewer = new Dynamsoft.WebViewer.DocumentViewer(options,
                bridge,
                new Uri("http://127.0.0.1:18622"));

            await _documentViewer.EnsureInitializedAsync();
            Func<string,bool> callback = (name) =>
            {
                if (name == "loadFile") {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        PickAndShow();
                    });
                }
                return true;
            };
            bridge.RegisterCallback(callback); 
            _documentViewer.DocumentSaved += (sender, args) =>
            {
                try
                {
                    string filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "output_fromview.pdf");
                    File.WriteAllBytes(filePath, args.Content);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    if (ex.Message != null) {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await DisplayAlert("Alert", ex.Message, "OK");
                        });
                    }
                }
            };
        }

        public async void PickAndShow()
        {
            try
            {
                PickOptions options = new()
                {
                    PickerTitle = "Please select a PDF/image file"
                };
                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    if (result.FileName.EndsWith("pdf", StringComparison.OrdinalIgnoreCase) || 
                        result.FileName.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) ||
                        result.FileName.EndsWith("png", StringComparison.OrdinalIgnoreCase))
                    {
                        using var stream = await result.OpenReadAsync();
                        var bytes = await StreamToBytesAsync(stream);
                        await webView.EvaluateJavaScriptAsync($"loadImage('{Convert.ToBase64String(bytes)}');");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static async Task<byte[]> StreamToBytesAsync(Stream stream)
        {
            if (stream == null) return null;

            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }

        private async void OnActionItemClicked(object sender, EventArgs args)
        {
            string result = await DisplayActionSheet("Select an action","Cancel",null,"Scan with scanner", "Scan with camera", "Edit","Settings","Save as PDF","Open a local file");
            if (result == "Scan with scanner")
            {
                StartScanning("Document Scanner");
            }
            else if (result == "Scan with camera")
            {
                StartScanning("Camera");
            }
            else if (result == "Edit")
            {
                await _documentViewer.WebView.ExecuteJavaScriptAsync("showEditor();");
            }
            else if (result == "Settings") {
                await Shell.Current.GoToAsync("SettingsPage");
            }
            else if (result == "Save as PDF")
            {
                SaveFile();
            }
            else if (result == "Open a local file")
            {
                PickAndShow();
            }
        }

        private async void StartScanning(string action) {
            if (action == "Camera")
            {
                await _documentViewer.WebView.ExecuteJavaScriptAsync("startLiveScanning();");
            }
            else if (action == "Document Scanner")
            {
                var canceled = false;
                Func<object> cancelEvent = () =>
                {
                    canceled = true;
                    Navigation.PopModalAsync();
                    CancelScanning();
                    return "";
                };
                var page = new ProgressPage();
                page.RegisterCallback(cancelEvent);
                await Navigation.PushModalAsync(page);
                await ScanDocument();
                if (!canceled)
                {
                    await Navigation.PopModalAsync();
                }

            }
        }

        private async void SaveFile() {
            try
            {
                PageOption pageOption = PageOption.All; // Default to "Save Current Page"
                PdfPageType pdfPageType = PdfPageType.PageDefault;
                SaveAnnotationMode annotationMode = SaveAnnotationMode.None;
                byte[] pdfContent = await _documentViewer.SaveAsPdf(pageOption,pdfPageType,annotationMode,"");
                string targetFile = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "out.pdf");
                await using (var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write))
                {
                    await fileStream.WriteAsync(pdfContent, 0, pdfContent.Length);
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "Share PDF file",
                        File = new ShareFile(targetFile)
                    });
                }
            }
            catch (Exception ex)
            {
                if (ex.Message != null)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Alert", ex.Message, "OK");
                    });
                }
            }

        }

        private void CancelScanning() {
            scannerJob?.DeleteJob();
        }

        private async Task<bool> ScanDocument()
        {
            try
            {
                var license = Preferences.Get("License", productKey);
                if (license == "") {
                    license = productKey;
                }
                var DPI = Preferences.Get("DPI", 150);
                var colorMode = Preferences.Get("ColorMode", "Color");
                var IPAddress = Preferences.Get("IP", "https://127.0.0.1:18623");
                var scannerName = Preferences.Get("Scanner", "");
                var autoFeeder = Preferences.Get("AutoFeeder", false);
                var duplex = Preferences.Get("Duplex", false);
                var client = new DWTClient(new Uri(IPAddress), license);
                _documentViewer.DWTClient = client;
                CreateScanJobOptions options = new CreateScanJobOptions();
                options.AutoRun = false;
                options.RequireWebsocket = false;
                options.Config = new ScannerConfiguration();
                options.Config.XferCount = 7;
                options.Config.IfFeederEnabled = autoFeeder;
                options.Config.IfDuplexEnabled = duplex;
                Debug.WriteLine(colorMode);
                if (colorMode == "Color")
                {
                    Debug.WriteLine("scan in color");
                    options.Config.PixelType = EnumDWT_PixelType.TWPT_RGB;
                }
                else if (colorMode == "BlackWhite")
                {
                    Debug.WriteLine("scan in black white");
                    options.Config.PixelType = EnumDWT_PixelType.TWPT_BW;
                }
                else if (colorMode == "Grayscale")
                {
                    Debug.WriteLine("scan in grayscale");
                    options.Config.PixelType = EnumDWT_PixelType.TWPT_GRAY;
                }
                if (!string.IsNullOrEmpty(scannerName))
                {
                    var scanners = await _documentViewer.DWTClient.ScannerControlClient.ScannerManager.GetScanners(EnumDeviceTypeMask.DT_TWAINSCANNER | EnumDeviceTypeMask.DT_WIATWAINSCANNER);
                    foreach (var scanner in scanners)
                    {
                        if (scanner.Name == scannerName)
                        {
                            options.Device = scanner.Device;
                        }
                    }
                }
                scannerJob = await _documentViewer.CreateScanToViewJob(options);
                await _documentViewer.StartJob(scannerJob);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                if (ex.Message != null)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Alert", ex.Message, "OK");
                    });
                }
            }
            return true;
        }
    }

}

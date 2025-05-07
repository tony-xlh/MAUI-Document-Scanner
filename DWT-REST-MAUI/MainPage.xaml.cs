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

        public void RegisterCallback(Func<object, string, bool> callback)
        {
            _webView.RawMessageReceived += (sender, args) =>
            {
                // Parse the message and invoke the callback
                DynamicWebTWAIN.RestClient.JsonObject jsonObject;
                string content;
                Dynamsoft.WebViewer.DocumentViewer.ParseJavascriptResult(args.Message, out jsonObject, out content);
                callback?.Invoke(jsonObject, content);
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
            Func<object,string,bool> callback = (o,s) =>
            {
                JsonObject jsonObject = (JsonObject)o;
                object name = "";
                jsonObject.TryGetValue("name",out name);
                if ((string) name == "loadFile") {
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
                // The user canceled or something went wrong
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

        private async void OnSettingsItemClicked(object sender, EventArgs args) {
            await Shell.Current.GoToAsync("SettingsPage");
        }

        private void OnEditItemClicked(object sender, EventArgs args)
        {
            _documentViewer.ShowEditor();
        }

        private void OnOpenFileItemClicked(object sender, EventArgs args)
        {
            PickAndShow();
        }
        
        private async void OnSaveItemClicked(object sender, EventArgs args)
        {
            byte[] pdfContent = await _documentViewer.SaveAsPdf();
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

        private async void OnScanItemClicked(object sender, EventArgs args)
        {
            string action = await DisplayActionSheet("ActionSheet: Select the source", "Cancel", null , "Document Scanner", "Camera");
            if (action == "Camera")
            {
                await _documentViewer.WebView.ExecuteJavaScriptAsync("startLiveScanning();");
            }
            else if (action == "Document Scanner") {
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
                if (!canceled) {
                    await Navigation.PopModalAsync();
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
                var DPI = Preferences.Get("DPI", 150);
                var colorMode = Preferences.Get("ColorMode", "Color");
                var IPAddress = Preferences.Get("IP", "https://127.0.0.1:18623");
                var scannerName = Preferences.Get("Scanner", "");
                var autoFeeder = Preferences.Get("AutoFeeder", false);
                var duplex = Preferences.Get("Duplex", false);
                var client = new DWTClient(new Uri(IPAddress), license);
                _documentViewer.UpdateRESRClient(client);
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
                    var scanners = await _documentViewer.DWTClient.ScannerControlClient.Manager.Get(EnumDeviceTypeMask.DT_TWAINSCANNER | EnumDeviceTypeMask.DT_WIATWAINSCANNER);
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
            }
            return true;
        }
    }

}

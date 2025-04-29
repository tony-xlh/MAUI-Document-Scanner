using DynamicWebTWAIN.RestClient;
using Dynamsoft.WebViewer;
using Microsoft.Maui.Controls;
using System.Diagnostics;

namespace DWT_REST_MAUI
{
    public class WpfWebViewBridge : IWebViewBridge
    {
        private Microsoft.Maui.Controls.HybridWebView _webView;

        public WpfWebViewBridge(Microsoft.Maui.Controls.HybridWebView webView)
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
        public MainPage()
        {
            InitializeComponent();
            webView.SetInvokeJavaScriptTarget(this);
            InitViewer();
        }

        private async void InitViewer() {
            DocumentViewerOptions options = new DocumentViewerOptions();
            // because we load ddv page in the service, so we should make sure the service is running, so we need to set a long timeout
            // or manual create a websocket connection in js, recommend this way.
            options.ProductKey = "DLS2eyJoYW5kc2hha2VDb2RlIjoiMTAwMjI3NzYzLVRYbFFjbTlxIiwibWFpblNlcnZlclVSTCI6Imh0dHBzOi8vbWx0cy5keW5hbXNvZnQuY29tIiwib3JnYW5pemF0aW9uSUQiOiIxMDAyMjc3NjMiLCJzdGFuZGJ5U2VydmVyVVJMIjoiaHR0cHM6Ly9zbHRzLmR5bmFtc29mdC5jb20iLCJjaGVja0NvZGUiOjE4OTc4MDUzNDV9";
            options.SiteUrl = "index.html";
            options.MessageType = "__RawMessage";
            _documentViewer = new Dynamsoft.WebViewer.DocumentViewer(options,
                new WpfWebViewBridge(webView),
                new Uri("http://127.0.0.1:18622"));

            await _documentViewer.EnsureInitializedAsync();

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

        private async void OnStartScanButtonClicked(object sender, EventArgs args)
        {
            try
            {
                var scanners = await _documentViewer.DWTClient.ScannerControlClient.Manager.Get(EnumDeviceTypeMask.DT_TWAINSCANNER);
                CreateScanJobOptions options = new CreateScanJobOptions();
                options.Device = scanners[1].Device;
                options.AutoRun = false;
                options.RequireWebsocket = false;
                options.Config = new ScannerConfiguration();
                options.Config.XferCount = 7;
                options.Config.IfFeederEnabled = true;
                options.Config.IfDuplexEnabled = true;
                await _documentViewer.ScanImageToView(options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }

}

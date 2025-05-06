using Microsoft.Maui.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWT_REST_MAUI.Platforms.Android
{
    public class MyHybridWebViewHandler : HybridWebViewHandler
    {

        protected override global::Android.Webkit.WebView CreatePlatformView()
        {
            var view = base.CreatePlatformView();
            view.Settings.MediaPlaybackRequiresUserGesture = false;
            var client = new MyWebChromeClient(this.Context);
            view.SetWebChromeClient(client);
            return view;
        }
    }
}

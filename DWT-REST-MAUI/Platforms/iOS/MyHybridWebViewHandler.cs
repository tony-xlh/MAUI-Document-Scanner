using Microsoft.Maui.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWT_REST_MAUI.Platforms.iOS
{
    class MyHybridWebViewHandler : HybridWebViewHandler
    {
        protected override WebKit.WKWebView CreatePlatformView()
        {
            var view = base.CreatePlatformView();
            view.Configuration.AllowsInlineMediaPlayback = true;
            view.Configuration.MediaTypesRequiringUserActionForPlayback = WebKit.WKAudiovisualMediaTypes.None;
            return view;
        }
    }
}

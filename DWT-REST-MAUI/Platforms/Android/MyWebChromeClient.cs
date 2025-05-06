using Android.Content;
using Android.Webkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWT_REST_MAUI.Platforms.Android
{
    public class MyWebChromeClient : WebChromeClient
    {
        private MainActivity _activity;

        public MyWebChromeClient(Context context)
        {
            _activity = context as MainActivity;
        }

        public override void OnPermissionRequest(PermissionRequest request)
        {
            try
            {
                request.Grant(request.GetResources());
                base.OnPermissionRequest(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }
}

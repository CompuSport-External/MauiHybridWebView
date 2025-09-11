using Android.Views;
using Android.Webkit;
using Java.Interop;
using AWebView = Android.Webkit.WebView;

namespace HybridWebView
{
    partial class HybridWebView
    {
        private HybridWebViewJavaScriptInterface? _javaScriptInterface;

        private AWebView PlatformWebView => (AWebView)Handler?.PlatformView!;


        private partial Task InitializeHybridWebView()
        {
            // Note that this is a per-app setting and not per-control, so if you enable
            // this, it is enabled for all Android WebViews in the app.
            AWebView.SetWebContentsDebuggingEnabled(enabled: true);

            if (PlatformWebView == null)
                return Task.CompletedTask;

            PlatformWebView.OverScrollMode = OverScrollMode.Never;
            //PlatformWebView.Settings.SetAppCacheEnabled(true);
            PlatformWebView.Settings.BuiltInZoomControls = true;
            PlatformWebView.Settings.DisplayZoomControls = false;
            //Fixe texte du bracket si zoomed out
            PlatformWebView.Settings.MinimumFontSize = 1;

            PlatformWebView.Settings.SetGeolocationEnabled(true);
            //e.Settings.SetGeolocationDatabasePath(e.Context.FilesDir.Path);

            //Fixe accessibility size increase
            PlatformWebView.Settings.TextZoom = 100;

            if (_javaScriptInterface == null)
            {
                _javaScriptInterface = new HybridWebViewJavaScriptInterface(this);
            }
            PlatformWebView.AddJavascriptInterface(_javaScriptInterface, "hybridWebViewHost");

            Android.Webkit.CookieManager.Instance?.SetAcceptCookie(true);
            Android.Webkit.CookieManager.Instance?.SetAcceptThirdPartyCookies(PlatformWebView, true);

            return Task.CompletedTask;
        }

        private partial void NavigateCore(string url)
        {
            var cookieManager = Android.Webkit.CookieManager.Instance;
            if (cookieManager != null)
            {
                foreach (var item in HybridWebView.AllRequestsCookies)
                {
                    var val = $"{item.Key}={item.Value}";
                    cookieManager.SetCookie("/", val);
                }
                cookieManager.Flush();
            }

            if (string.IsNullOrEmpty(url))
                return;

            PlatformWebView.LoadUrl(new Uri(url).ToString(), HybridWebView.AdditionalHeaders);
        }

        public partial Task ClearAllCookiesAsync()
        {
            var cookieManager = Android.Webkit.CookieManager.Instance;
            if (cookieManager == null)
                return Task.CompletedTask;

            // Ne pas faire car ca flush les cookies de antiforgery
            // cookieManager.RemoveAllCookies(null);

            foreach (var item in HybridWebView.AllRequestsCookies)
            {
                var val = $"{item.Key}={item.Value}";
                cookieManager.SetCookie("/", val);
            }

            cookieManager.RemoveSessionCookies(null); // Use RemoveSessionCookies instead of RemoveExpiredCookie
            cookieManager.Flush();

            PlatformWebView.ClearCache(true);

            return Task.CompletedTask;
        }

        private sealed class HybridWebViewJavaScriptInterface : Java.Lang.Object
        {
            private readonly HybridWebView _hybridWebView;

            public HybridWebViewJavaScriptInterface(HybridWebView hybridWebView)
            {
                _hybridWebView = hybridWebView;
            }

            [JavascriptInterface]
            [Export("sendMessage")]
            public void SendMessage(string message)
            {
                _hybridWebView.OnMessageReceived(message);
            }
        }
    }
}

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using System.Reflection;

namespace HybridWebView
{
    public partial class HybridWebViewHandler : WebViewHandler
    {
        public static IPropertyMapper<IWebView, IWebViewHandler> HybridWebViewMapper = new PropertyMapper<IWebView, IWebViewHandler>(WebViewHandler.Mapper)
        {
#if __ANDROID__
            [nameof(Android.Webkit.WebViewClient)] = MapHybridWebViewClient,
            [nameof(Android.Webkit.WebChromeClient)] = MapHybridWebChromeClient,
#endif
        };

        public HybridWebViewHandler() : base(HybridWebViewMapper, CommandMapper)
        {
        }

        public HybridWebViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
            : base(mapper ?? HybridWebViewMapper, commandMapper ?? CommandMapper)
        {
        }

#if ANDROID

        private static Android.Webkit.WebView? _platformWebView;
        public bool IsRestoringState { get; private set; }   // changed to public for external readiness checks
        internal void FinishRestore() => IsRestoringState = false; // called by client

        private AndroidHybridWebViewClient? _client;
        private HybridWebChromeClient? _chromeClient;

        protected override Android.Webkit.WebView CreatePlatformView()
        {
            if (_platformWebView != null)
            {
                (_platformWebView.Parent as Android.Views.ViewGroup)?.RemoveView(_platformWebView);
                var handlerField = typeof(MauiWebView).GetField("_handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
                handlerField?.SetValue(_platformWebView, this);
                IsRestoringState = true;
                return _platformWebView;
            }
            _platformWebView = base.CreatePlatformView(); // let base create the view
            if (OperatingSystem.IsAndroidVersionAtLeast(23) && Context?.ApplicationInfo?.Flags.HasFlag(Android.Content.PM.ApplicationInfoFlags.HardwareAccelerated) == false)
            {
                _platformWebView.SetLayerType(Android.Views.LayerType.Software, null);
            }
            return _platformWebView;
        }

        WebViewSource? _cachedSource;     // keep it so databinding isn’t broken
        string _cachedStartPath;

        public override void SetVirtualView(IView view)
        {
            bool reattach = _platformWebView != null;   // we’re re-using the old WebView

            if (reattach && view is HybridWebView wv)
            {
                _cachedSource = wv.Source;  // remember the original value
                _cachedStartPath = wv.StartPath;
                wv.Source = null;       // hide it from ProcessSourceWhenReady
                wv.StartPath = string.Empty;
            }

            base.SetVirtualView(view);      // MAUI won’t try to navigate

            if (reattach && view is HybridWebView wv2)
            {
                //wv2.Source = _cachedSource;  // Can't reset the Source since it navigates
                wv2.StartPath = _cachedStartPath;
            }
        }

        protected override void ConnectHandler(Android.Webkit.WebView platformView)
        {
            base.ConnectHandler(platformView);
        }

        private static bool TryGetPlatformView(IWebViewHandler handler, out Android.Webkit.WebView platformView)
        {
            try
            {
                platformView = handler.PlatformView
                    ?? throw new InvalidOperationException("PlatformView cannot be null.");
                return true;
            }
            catch (InvalidOperationException ioe) when (ioe.Message?.Contains("PlatformView cannot be null", StringComparison.OrdinalIgnoreCase) == true)
            {
                platformView = null!;
                return false;
            }
        }

        public static void MapHybridWebViewClient(IWebViewHandler handler, IWebView webView)
        {
            if (handler is not HybridWebViewHandler platformHandler || !TryGetPlatformView(handler, out var platformView))
                return;

            if (platformHandler._client is AndroidHybridWebViewClient existing)
            {
                // --- 1. update the strong reference inside our own class
                typeof(AndroidHybridWebViewClient)
                   .GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)!
                   .SetValue(existing, platformHandler);

                // --- 2. update the weak reference inside the base class
                typeof(MauiWebViewClient)
                   .GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!
                   .SetValue(existing, new WeakReference<WebViewHandler?>(platformHandler));

                return; // client already present, nothing else to do
            }

            // Otherwise attach a fresh client (first time only)
            var client = new AndroidHybridWebViewClient(platformHandler);
            platformView.SetWebViewClient(client);
            platformHandler._client = client;

            // wire the base-class field once
            typeof(MauiWebViewClient)
               .GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!
               .SetValue(client, new WeakReference<WebViewHandler?>(platformHandler));
        }

        public static void MapHybridWebChromeClient(IWebViewHandler handler, IWebView webView)
        {
            if (handler is not HybridWebViewHandler platformHandler || !TryGetPlatformView(handler, out var platformView))
                return;

            if (platformHandler._chromeClient is null)
            {
                platformHandler._chromeClient = new HybridWebChromeClient(platformHandler);
                platformView.SetWebChromeClient(platformHandler._chromeClient);
            }
            else if (handler is WebViewHandler viewHandler)
            {
                var handlerField = typeof(Android.Webkit.WebChromeClient).GetField("_handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
                handlerField?.SetValue(platformHandler._chromeClient, viewHandler);
            }
        }

        protected override void DisconnectHandler(Android.Webkit.WebView platformView)
        {
            (platformView.Parent as Android.Views.ViewGroup)?.RemoveView(platformView);
        }
#endif
    }
}

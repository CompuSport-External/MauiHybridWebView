using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using System.Drawing;
using System.Globalization;
using System.Reflection.Metadata;
using System.Runtime.Versioning;
using WebKit;

namespace HybridWebView
{
    partial class HybridWebViewHandler
    {
        protected override WKWebView CreatePlatformView()
        {
            var config = new WKWebViewConfiguration();
            config.UserContentController.AddScriptMessageHandler(new WebViewScriptMessageHandler(MessageReceived), "webwindowinterop");
            // config.SetUrlSchemeHandler(new SchemeHandler(this), urlScheme: "app");

            // Legacy Developer Extras setting.
            var enableWebDevTools = ((HybridWebView)VirtualView).EnableWebDevTools;
            config.Preferences.SetValueForKey(NSObject.FromObject(enableWebDevTools), new NSString("developerExtrasEnabled"));
            config.WebsiteDataStore = WKWebsiteDataStore.DefaultDataStore;

            var platformView = new MauiWKWebView(RectangleF.Empty, this, config);
            //platformView.NavigationDelegate = new CSMauiWebViewNavigationDelegate(this);

            if (OperatingSystem.IsMacCatalystVersionAtLeast(major: 13, minor: 3) ||
                OperatingSystem.IsIOSVersionAtLeast(major: 16, minor: 4))
            {
                // Enable Developer Extras for Catalyst/iOS builds for 16.4+
                platformView.SetValueForKey(NSObject.FromObject(enableWebDevTools), new NSString("inspectable"));
            }

            return platformView;
        }

        private void MessageReceived(Uri uri, string message)
        {
            ((HybridWebView)VirtualView).OnMessageReceived(message);
        }

        public class CustomNavigationDelegate : WKNavigationDelegate
        {
            public override void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
            {
                Console.WriteLine($"[iOS WebView] Provisional navigation failed: {error?.LocalizedDescription}");
                // You can trigger C# callbacks or messaging here
                //base.DidFailProvisionalNavigation(webView, navigation, error);
            }

            public override void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
            {
                Console.WriteLine($"[iOS WebView] Navigation failed: {error?.LocalizedDescription}");
                //base.DidFailNavigation(webView, navigation, error);
            }
        }

        public class CSMauiWebViewNavigationDelegate : NSObject, IWKNavigationDelegate
        {
            readonly WeakReference<IWebViewHandler> _handler;
            WebNavigationEvent _lastEvent;

            public CSMauiWebViewNavigationDelegate(IWebViewHandler handler)
            {
                _ = handler ?? throw new ArgumentNullException(nameof(handler));
                _handler = new WeakReference<IWebViewHandler>(handler);
            }

            [Export("webView:didFinishNavigation:")]
            public void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
            {
                var handler = Handler;

                if (handler is null || !handler.IsConnected())
                    return;

                var platformView = handler?.PlatformView;
                var virtualView = handler?.VirtualView;

                if (platformView is null || virtualView is null)
                    return;

                //platformView.UpdateCanGoBackForward(virtualView);

                if (webView.IsLoading)
                    return;

                var url = GetCurrentUrl();

                if (url == $"file://{NSBundle.MainBundle.BundlePath}/")
                    return;

                virtualView.Navigated(_lastEvent, url, WebNavigationResult.Success);

                // ProcessNavigatedAsync calls UpdateCanGoBackForward
                //if (handler is WebViewHandler webViewHandler)
                //    webViewHandler.ProcessNavigatedAsync(url).FireAndForget();
                //else
                //    platformView.UpdateCanGoBackForward(virtualView);
            }

            [Export("webView:didFailNavigation:withError:")]
            public void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
            {
                var handler = Handler;

                if (handler is null || !handler.IsConnected())
                    return;

                var platformView = handler?.PlatformView;
                var virtualView = handler?.VirtualView;

                if (platformView is null || virtualView is null)
                    return;

                var url = GetCurrentUrl();

                virtualView.Navigated(_lastEvent, url, WebNavigationResult.Failure);

                //platformView.UpdateCanGoBackForward(virtualView);
            }

            [Export("webView:didFailProvisionalNavigation:withError:")]
            public void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
            {
                var handler = Handler;

                if (handler is null || !handler.IsConnected())
                    return;

                var platformView = handler?.PlatformView;
                var virtualView = handler?.VirtualView;

                if (platformView is null || virtualView is null)
                    return;

                var url = GetCurrentUrl();

                virtualView.Navigated(_lastEvent, url, WebNavigationResult.Failure);

                //platformView.UpdateCanGoBackForward(virtualView);
            }

            // https://stackoverflow.com/questions/37509990/migrating-from-uiwebview-to-wkwebview
            [Export("webView:decidePolicyForNavigationAction:decisionHandler:")]
            public void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
            {
                var handler = Handler;

                if (handler is null || !handler.IsConnected())
                {
                    decisionHandler.Invoke(WKNavigationActionPolicy.Cancel);
                    return;
                }

                var platformView = handler?.PlatformView;
                var virtualView = handler?.VirtualView;

                if (platformView is null || virtualView is null)
                {
                    decisionHandler.Invoke(WKNavigationActionPolicy.Cancel);
                    return;
                }

                // Only surface main-frame navigations
                var targetFrame = navigationAction.TargetFrame;
                bool isMainFrame = targetFrame?.MainFrame ?? true; // null sometimes means new window; treat as main-frame

                var request = navigationAction.Request;
                var url = request?.Url;
                var abs = url?.AbsoluteString ?? string.Empty;
                var scheme = url?.Scheme ?? string.Empty;

                // Internal/placeholder navigations we should allow but *not* notify about
                bool isInternalNav =
                    string.Equals(abs, "about:blank", StringComparison.OrdinalIgnoreCase) ||
                    abs.StartsWith("about:srcdoc", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(scheme, "blob", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(scheme, "data", StringComparison.OrdinalIgnoreCase);

                // If it's not the main frame OR it's an internal URL (like about:blank), just allow and exit early
                if (!isMainFrame || isInternalNav)
                {
                    decisionHandler.Invoke(WKNavigationActionPolicy.Allow);
                    return;
                }

                var navEvent = WebNavigationEvent.NewPage;

                switch (navigationAction.NavigationType)
                {
                    case WKNavigationType.LinkActivated:
                        navEvent = WebNavigationEvent.NewPage;

                        // If target is null, WebKit wants a new window; load into this webview instead
                        if (navigationAction.TargetFrame == null && request != null)
                            webView?.LoadRequest(request);
                        break;

                    case WKNavigationType.FormSubmitted:
                    case WKNavigationType.FormResubmitted:
                        navEvent = WebNavigationEvent.NewPage;
                        break;

                    case WKNavigationType.BackForward:
                        navEvent = CurrentNavigationEvent;
                        break;

                    case WKNavigationType.Reload:
                        navEvent = WebNavigationEvent.Refresh;
                        break;

                    case WKNavigationType.Other:
                        navEvent = WebNavigationEvent.NewPage;
                        break;
                }

                _lastEvent = navEvent;

                var lastUrl = abs;
                bool cancel = virtualView.Navigating(navEvent, lastUrl);

                decisionHandler(cancel ? WKNavigationActionPolicy.Cancel : WKNavigationActionPolicy.Allow);
            }


            string GetCurrentUrl()
            {
                return Handler?.PlatformView?.Url?.AbsoluteUrl?.ToString() ?? string.Empty;
            }

            internal WebNavigationEvent CurrentNavigationEvent
            {
                get;
                set;
            }

            IWebViewHandler? Handler
            {
                get
                {
                    if (_handler.TryGetTarget(out var handler))
                    {
                        return handler;
                    }

                    return null;
                }
            }
        }

        private sealed class WebViewScriptMessageHandler : NSObject, IWKScriptMessageHandler
        {
            private Action<Uri, string> _messageReceivedAction;

            public WebViewScriptMessageHandler(Action<Uri, string> messageReceivedAction)
            {
                _messageReceivedAction = messageReceivedAction ?? throw new ArgumentNullException(nameof(messageReceivedAction));
            }

            public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
            {
                if (message is null)
                {
                    throw new ArgumentNullException(nameof(message));
                }
                _messageReceivedAction(null, ((NSString)message.Body).ToString());
            }
        }

        //private class SchemeHandler : NSObject, IWKUrlSchemeHandler
        //{
        //    private readonly HybridWebViewHandler _webViewHandler;

        //    public SchemeHandler(HybridWebViewHandler webViewHandler)
        //    {
        //        _webViewHandler = webViewHandler;
        //    }

        //    [Export("webView:startURLSchemeTask:")]
        //    [SupportedOSPlatform("ios11.0")]
        //    public async void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
        //    {
        //        var url = urlSchemeTask.Request.Url?.AbsoluteString ?? "";

        //        var responseData = await GetResponseBytes(url);

        //        if (responseData.StatusCode == 200)
        //        {
        //            using (var dic = new NSMutableDictionary<NSString, NSString>())
        //            {
        //                dic.Add((NSString)"Content-Length", (NSString)(responseData.ResponseBytes.Length.ToString(CultureInfo.InvariantCulture)));
        //                dic.Add((NSString)"Content-Type", (NSString)responseData.ContentType);
        //                // Disable local caching. This will prevent user scripts from executing correctly.
        //                dic.Add((NSString)"Cache-Control", (NSString)"no-cache, max-age=0, must-revalidate, no-store");
        //                if (urlSchemeTask.Request.Url != null)
        //                {
        //                    using var response = new NSHttpUrlResponse(urlSchemeTask.Request.Url, responseData.StatusCode, "HTTP/1.1", dic);
        //                    urlSchemeTask.DidReceiveResponse(response);
        //                }
        //            }

        //            urlSchemeTask.DidReceiveData(NSData.FromArray(responseData.ResponseBytes));
        //            urlSchemeTask.DidFinish();
        //        }
        //    }

        //    private async Task<(byte[] ResponseBytes, string ContentType, int StatusCode)> GetResponseBytes(string url)
        //    {
        //        string contentType;

        //        string fullUrl = url;
        //        url = QueryStringHelper.RemovePossibleQueryString(url);

        //        if (new Uri(url) is Uri uri && HybridWebView.AppOriginUri.IsBaseOf(uri))
        //        {
        //            var relativePath = HybridWebView.AppOriginUri.MakeRelativeUri(uri).ToString().Replace('\\', '/');

        //            var hwv = (HybridWebView)_webViewHandler.VirtualView;

        //            var bundleRootDir = Path.Combine(NSBundle.MainBundle.ResourcePath, hwv.HybridAssetRoot!);

        //            if (string.IsNullOrEmpty(relativePath))
        //            {
        //                relativePath = hwv.MainFile!.Replace('\\', '/');
        //                contentType = "text/html";
        //            }
        //            else
        //            {
        //                var requestExtension = Path.GetExtension(relativePath);
        //                contentType = requestExtension switch
        //                {
        //                    ".htm" or ".html" => "text/html",
        //                    ".js" => "application/javascript",
        //                    ".css" => "text/css",
        //                    _ => "text/plain",
        //                };
        //            }

        //            Stream? contentStream = null;

        //            // Check to see if the request is a proxy request.
        //            if (relativePath == HybridWebView.ProxyRequestPath)
        //            {
        //                var args = new HybridWebViewProxyEventArgs(fullUrl);

        //                await hwv.OnProxyRequestMessage(args);

        //                if (args.ResponseStream != null)
        //                {
        //                    contentType = args.ResponseContentType ?? "text/plain";
        //                    contentStream = args.ResponseStream;
        //                }
        //            }

        //            if (contentStream == null)
        //            {
        //                contentStream = KnownStaticFileProvider.GetKnownResourceStream(relativePath!);
        //            }

        //            if (contentStream is not null)
        //            {
        //                using var ms = new MemoryStream();
        //                contentStream.CopyTo(ms);
        //                return (ms.ToArray(), contentType, StatusCode: 200);
        //            }

        //            var assetPath = Path.Combine(bundleRootDir, relativePath);

        //            if (File.Exists(assetPath))
        //            {
        //                return (File.ReadAllBytes(assetPath), contentType, StatusCode: 200);
        //            }
        //        }

        //        return (Array.Empty<byte>(), ContentType: string.Empty, StatusCode: 404);
        //    }

        //    [Export("webView:stopURLSchemeTask:")]
        //    public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
        //    {
        //    }
        //}
    }
}

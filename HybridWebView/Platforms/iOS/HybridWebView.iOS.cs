using Foundation;
using UIKit;
using WebKit;
using Microsoft.Maui.Controls;

namespace HybridWebView
{
    partial class HybridWebView
    {
        private WKWebView PlatformWebView => (WKWebView)Handler!.PlatformView!;

        private NSObject? _didBecomeActiveObserver;

        private partial Task InitializeHybridWebView()
        {
            _didBecomeActiveObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidBecomeActiveNotification, HandleDidBecomeActive);
            return Task.CompletedTask;
        }

        private void HandleDidBecomeActive(NSNotification notification)
        {
            if (PlatformWebView.Url == null || PlatformWebView.Url.AbsoluteString == "about:blank")
            {
                Microsoft.Maui.Controls.Device.BeginInvokeOnMainThread(() => Navigate(StartPath));
            }
        }

        private partial void NavigateCore(string url)
        {
            using var nsUrl = new NSUrl(new Uri(url).ToString());
            using var request = new NSUrlRequest(nsUrl);

            PlatformWebView.LoadRequest(request);
        }

        public partial async Task ClearAllCookiesAsync()
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(11, 0))
            {
                var store = PlatformWebView.Configuration.WebsiteDataStore.HttpCookieStore;

                var cookies = await store.GetAllCookiesAsync();
                foreach (var c in cookies)
                {
                    await store.DeleteCookieAsync(c);
                }
            }

            //foreach (var domain in HybridWebView.AllRequestsCookies)
            //{
            //    var domainUrl = NSUrl.FromString(domain.Key);
            //    foreach (var cookie in NSHttpCookieStorage.SharedStorage.CookiesForUrl(domainUrl))
            //    {
            //        NSHttpCookieStorage.SharedStorage.DeleteCookie(cookie);
            //        _cookieDomains[domain.Key] = domain.Value - 1;
            //    }
            //}
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _didBecomeActiveObserver?.Dispose();
            _didBecomeActiveObserver = null;
        }
    }
}

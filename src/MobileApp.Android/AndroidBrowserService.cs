// global namespace alias qualifiers
using AndroidNet = Android.Net;
using AndroidContent = Android.Content;
using AndroidAppRoot = Android.App;

namespace MobileApp.Android;

public class AndroidBrowserService : IBrowserService
{
    public void OpenUrl(string url)
    {
        var uri = AndroidNet.Uri.Parse(url);
        var intent = new AndroidContent.Intent(AndroidContent.Intent.ActionView, uri);
        intent.AddFlags(AndroidContent.ActivityFlags.NewTask);
        AndroidAppRoot.Application.Context.StartActivity(intent);
    }
}

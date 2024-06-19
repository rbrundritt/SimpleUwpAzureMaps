using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ServiceModel.Channels;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SimpleUWPMapApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        // Using an IP address means that WebView2 doesn't wait for any DNS resolution,
        // making it substantially faster. Note that this isn't real HTTP traffic, since
        // we intercept all the requests within this origin.
        private static readonly string AppHostAddress = "0.0.0.0";

        /// <summary>
        /// Gets the application's base URI. Defaults to <c>https://0.0.0.0/</c>
        /// </summary>
        private static readonly Uri AppOriginUri = new Uri($"https://{AppHostAddress}/");

        /// <summary>
        /// A Reference to where the local web assets are stored.
        /// </summary>
        private static StorageFolder LocalWebAssetFolder;

        public MainPage()
        {
            this.InitializeComponent();           

            myWebView.CoreWebView2Initialized += MyWebView_CoreWebView2Initialized;
        }

        private async void MyWebView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            //Get files from the html folder within Assets folder.
            LocalWebAssetFolder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets\\html");

            //Setup the WebView2 to intercept requests to the AppHostAddress. This will allow us to load local files from the Assets folder.
            myWebView.CoreWebView2.AddWebResourceRequestedFilter($"https://{AppHostAddress}*", CoreWebView2WebResourceContext.All);
            myWebView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;

            //Optional: Make it easier for us to debug the WebView2 content by enabling the DevTools.
            myWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;

            //Optional: Add support for receiving messages from the WebView2 content.
            myWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
            myWebView.WebMessageReceived += MyWebView_WebMessageReceived;
        }

        private async void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            // Get a deferral object so that WebView2 knows there's some async stuff going on. We call Complete() at the end of this method.
            using (var deferral = args.GetDeferral())
            {
                try
                {
                    //Get the relative path of the requested file.
                    var relativePath = AppOriginUri.MakeRelativeUri(new Uri(args.Request.Uri)).ToString().Replace('/', '\\');

                    //Get the file from the local assets folder.
                    var file = await LocalWebAssetFolder.GetFileAsync(relativePath);

                    //Verify it exists.
                    if (File.Exists(file.Path))
                    {
                        using (var fileStream = File.OpenRead(file.Path))
                        {
                            //Determine what content type the file is.
                            var fileExtension = Path.GetExtension(relativePath);
                            string contentType;

                            switch (fileExtension)
                            {
                                case ".html":
                                    contentType = "text/html";
                                    break;
                                case ".jpg":
                                    contentType = "image/jpeg";
                                    break;
                                case ".png":
                                    contentType = "image/png";
                                    break;
                                case ".css":
                                    contentType = "text/css";
                                    break;
                                case ".js":
                                    contentType = "text/javascript";
                                    break;
                                default:
                                    contentType = "text/plain";
                                    break;
                            }

                            //Copy the file stream to a random access stream.
                            var randomAccessStream = new InMemoryRandomAccessStream();
                            using(var memStream = new MemoryStream()){
                                await fileStream.CopyToAsync(memStream);
                                await randomAccessStream.WriteAsync(memStream.GetWindowsRuntimeBuffer());
                            }

                            //Response with the file content.
                            args.Response = myWebView.CoreWebView2.Environment.CreateWebResourceResponse(randomAccessStream, 200, "OK", "Content-Type: " + contentType);
                        }
                    }
                    else
                    {
                        args.Response = myWebView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not found", "");
                    }
                }
                catch (Exception ex)
                {
                    args.Response = myWebView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not found", "");
                }

                // Notify WebView2 that the deferred (async) operation is complete and we set a response.
                deferral.Complete();
            }
        }
        private async void MyWebView_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            //Message from JavaScript recieved. 
            var dialog = new MessageDialog(args.TryGetWebMessageAsString());
            await dialog.ShowAsync();

            //NOTE: you can pass JSON as a string to here for more complex scenarios.
        }

        private static Random random = new Random();
        private async void InvokeJsBtn_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //Get random lat, lon coordinates.
            double lat = random.NextDouble() * 170 - 85;
            double lon = random.NextDouble() * 360 - 180;

            //Get random zoom level between 0 and 4 (don't want to be zoomed too close or we will get lost).
            int zoom = random.Next(0, 5);

            string jsonCameraOptions = $"{{ center:[{lon},{lat}], zoom: {zoom}}}";

            //Calla javascript function and pass information.
            await myWebView.ExecuteScriptAsync($"setMapView({jsonCameraOptions})");
        }
    }
}

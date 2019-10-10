using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics.Display;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CustomVision.Lego
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const string predictionKey = "";
        const string CustomVisionEndpoint = "https://southcentralus.api.cognitive.microsoft.com";
        Guid projectId = new System.Guid("");

        MediaCapture mediaCapture;
        bool isPreviewing;
        DisplayRequest displayRequest = new DisplayRequest();

        enum States
        {
            Initializing, 
            Waiting,
            Processing
        }

        private States AppState = States.Initializing;

        public MainPage()
        {
            this.InitializeComponent();

            Application.Current.Suspending += Application_Suspending;
        }


        private async Task StartPreviewAsync()
        {
            try
            {
                mediaCapture = new MediaCapture();

                await mediaCapture.InitializeAsync();

                var settings = new ZoomSettings();
                settings.Mode = ZoomTransitionMode.Smooth;
                settings.Value = 10;

                mediaCapture.VideoDeviceController.ZoomControl.Configure(settings);

                displayRequest.RequestActive();
                
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
            }
            catch (UnauthorizedAccessException)
            {
                // This will be thrown if the user denied access to the camera in privacy settings
                ShowMessageToUser("The app was denied access to the camera");
                return;
            }

            try
            {
                PreviewControl.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
            }
            catch (System.IO.FileLoadException)
            {
                mediaCapture.CaptureDeviceExclusiveControlStatusChanged += _mediaCapture_CaptureDeviceExclusiveControlStatusChanged;
            }

            AppState = States.Waiting;
        }

        private async void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();
                await CleanupCameraAsync();
                deferral.Complete();
            }
        }

        private void ShowMessageToUser(string v)
        {
            throw new NotImplementedException();
        }

        private async void _mediaCapture_CaptureDeviceExclusiveControlStatusChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
        {
            if (args.Status == MediaCaptureDeviceExclusiveControlStatus.SharedReadOnlyAvailable)
            {
                ShowMessageToUser("The camera preview can't be displayed because another app has exclusive access");
            }
            else if (args.Status == MediaCaptureDeviceExclusiveControlStatus.ExclusiveControlAvailable && !isPreviewing)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await StartPreviewAsync();
                });
            }
        }

        private async Task CleanupCameraAsync()
        {
            if (mediaCapture != null)
            {
                if (isPreviewing)
                {
                    await mediaCapture.StopPreviewAsync();
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    PreviewControl.Source = null;
                    if (displayRequest != null)
                    {
                        displayRequest.RequestRelease();
                    }

                    mediaCapture.Dispose();
                    mediaCapture = null;
                });
            }
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            await CleanupCameraAsync();
        }

        private async void TakePicture()
        {
            AppState = States.Processing;
            CountDown.Text = "Calculation...";

            CustomVisionPredictionClient endpoint = new CustomVisionPredictionClient()
            {
                ApiKey = predictionKey,
                Endpoint = CustomVisionEndpoint
            };

            StringBuilder sb = new StringBuilder();

            using (var captureStream = new InMemoryRandomAccessStream())
            {
                await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), captureStream);
                captureStream.Seek(0);
                StorageFile destinationFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync($@"{Guid.NewGuid()}.jpg");
                try
                {
                    using (var destinationStream = (await destinationFile.OpenAsync(FileAccessMode.ReadWrite)).GetOutputStreamAt(0))
                    {
                        await RandomAccessStream.CopyAndCloseAsync(captureStream, destinationStream);
                    }
                }   
                finally
                {
                    await destinationFile.DeleteAsync();
                }

                var stream = captureStream.AsStream();
                stream.Seek(0, SeekOrigin.Begin);

                // Make a prediction against the new project
                sb.Append("Prediction:\n");
                var predictions = new List<Prediction>();

                try
                {
                    var result = endpoint.PredictImage(projectId, stream);

                    foreach (var c in result.Predictions)
                    {
                        predictions.Add(new Prediction(c));
                        sb.AppendFormat($"{c.TagName}: {c.Probability:P1}\n");
                    }
                }
                catch (Exception ex)
                {
                    sb.Append(ex.Message);
                }

                Output.Text = sb.ToString();

                if (predictions.Count > 0)
                {

                    var highestPrediction = (from p in predictions orderby p.Probability descending select p).First();

                    try
                    {
                        ImageColor.Fill = highestPrediction.GetColorCode();
                    }
                    catch (Exception) { }

                    try
                    {
                        ImagePreview.Source = highestPrediction.GetPicture();
                    }
                    catch (Exception) { }
                }
                else
                {
                    Output.Text = "No prediction possible";
                }

                CountDown.Text = countDown.ToString();
            }
            AppState = States.Waiting;
        }

        DispatcherTimer dispatcherTimer;
        int countDown = 5;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            StartPreviewAsync();
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        void dispatcherTimer_Tick(object sender, object e)
        {
            if (AppState == States.Waiting)
            {
                countDown--;
                CountDown.Text = countDown.ToString();
                if (countDown <= 0)
                {
                    countDown = 10;
                    TakePicture();
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

            dispatcherTimer.Stop();
            CleanupCameraAsync();
        }
    }
}

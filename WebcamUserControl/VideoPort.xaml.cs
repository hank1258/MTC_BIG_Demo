using AForge.Video;
using AForge.Video.DirectShow;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WebcamUserControl;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using WebCamUserControl;
using OpenCvSharp;
using OpenCvSharp.Extensions;
namespace WebcamUserControl
{
    public partial class VideoPortControl : UserControl
    {
        Microsoft.ProjectOxford.Face.FaceServiceClient faceserviceclient = new FaceServiceClient("2c2a7f6eca9e4197926721a886786d6b");
        Microsoft.ProjectOxford.Face.FaceServiceClient faceClient = new FaceServiceClient("2c2a7f6eca9e4197926721a886786d6b");
        // Create grabber. 
       // FrameGrabber<Face[]> grabber = new FrameGrabber<Face[]>();
        private readonly FrameGrabber<LiveCameraResult> _grabber = null;
        private LiveCameraResult _latestResultsToDisplay = null;
        private bool _fuseClientRemoteResults;
        private static readonly ImageEncodingParam[] s_jpegParams = {
            new ImageEncodingParam(OpenCvSharp.ImageEncodingID.JpegQuality,60) //ImwriteFlags.JpegQuality, 60)
        };
        public VideoPortControl()
        {
            InitializeComponent();
            // Create grabber. 
            _grabber = new FrameGrabber<LiveCameraResult>();
            // Set up a listener for when the client receives a new frame.
            _grabber.NewFrameProvided += (s, e) =>
            {
               // Console.WriteLine("ddvvvvvvvvvvvvvvvvvvvvvvvvv");
                // The callback may occur on a different thread, so we must use the
                // MainWindow.Dispatcher when manipulating the UI. 
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    var device = (FilterInfo)VideoDevicesComboBox.SelectedItem;
                    // Display the image in the left pane.
                     LeftImage.Source = e.Frame.Image.ToBitmapSource();
                 //   Console.WriteLine("ddddddddddddddddd");
                    //videoSourcePlayer.VideoSource= e.Frame.Image.ToBitmapSource();

                    // If we're fusing client-side face detection with remote analysis, show the
                    // new frame now with the most recent analysis available. 
                    /* if (_fuseClientRemoteResults)
                     {
                         RightImage.Source = VisualizeResult(e.Frame);
                     }*/
                }));

                // See if auto-stop should be triggered. 
             /*   if (Properties.Settings.Default.AutoStopEnabled && (DateTime.Now - _startTime) > Properties.Settings.Default.AutoStopTime)
                {
                    _grabber.StopProcessingAsync();
                }*/
            };

            // Set up a listener for when the client receives a new result from an API call. 
            _grabber.NewResultAvailable += (s, e) =>
            {
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (e.TimedOut)
                    {
                        // MessageArea.Text = "API call timed out.";
                        Console.WriteLine("api time out");
                    }
                    else if (e.Exception != null)
                    {
                        string apiName = "";
                        string message = e.Exception.Message;
                        var faceEx = e.Exception as FaceAPIException;
                        var emotionEx = e.Exception as Microsoft.ProjectOxford.Common.ClientException;
                        var visionEx = e.Exception as Microsoft.ProjectOxford.Vision.ClientException;
                        if (faceEx != null)
                        {
                            apiName = "Face";
                            message = faceEx.ErrorMessage;
                        }
                        else if (emotionEx != null)
                        {
                            apiName = "Emotion";
                            message = emotionEx.Error.Message;
                        }
                        else if (visionEx != null)
                        {
                            apiName = "Computer Vision";
                            message = visionEx.Error.Message;
                        }
                       // MessageArea.Text = string.Format("{0} API call failed on frame {1}. Exception: {2}", apiName, e.Frame.Metadata.Index, message);
                    }
                    else
                    {
                        _latestResultsToDisplay = e.Analysis;

                        // Display the image and visualization in the right pane. 
                       /* if (!_fuseClientRemoteResults)
                        {
                            RightImage.Source = VisualizeResult(e.Frame);
                        }*/
                    }
                }));
            };
            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            foreach (var d in videoDevices)
                VideoDevicesComboBox.Items.Add(d);

            VideoDevicesComboBox.SelectedIndex = 0;

        }

        public string OverlayImagePath { get; set; }






        /// <summary>
        /// Displays webcam video and asks for image to overlay
        /// </summary>
        public async void StartVideoFeed()
        {
            var openFileDialog = new OpenFileDialog()
            {
                DefaultExt = "png",
                Filter = "PNG Image | *.png",
                Title = "Please select an image to overlay onto the video feed..."
            };

            if (openFileDialog.ShowDialog() == true)
                OverlayImagePath = openFileDialog.FileName;

            var device = (FilterInfo)VideoDevicesComboBox.SelectedItem;
            if (device != null)
            {
                var source = new VideoCaptureDevice(device.MonikerString);
                // register NewFrame event handler
                Console.WriteLine("ppppp");
            //    source.NewFrame += new NewFrameEventHandler(video_NewFrame);

             //   videoSourcePlayer.VideoSource = source;
              //  videoSourcePlayer.Start();
            }


            // How often to analyze. 
            _grabber.TriggerAnalysisOnInterval(Properties.Settings.Default.AnalysisInterval);
            _grabber.AnalysisFunction = FacesAnalysisFunction;
            await _grabber.StartProcessingCameraAsync(VideoDevicesComboBox.SelectedIndex);
        }

        /// <summary>
        /// Stops the display of webcam video.
        /// </summary>
        public void StopVideoFeed()
        {
            videoSourcePlayer.SignalToStop();
        }

        /// <summary>
        /// Saves a snapshot of current webcam video frame.
        /// </summary>
        public void SaveSnapshot()
        {
            using (Bitmap bmp = videoSourcePlayer.GetCurrentVideoFrame())
            {
                var saveFileDialog = new SaveFileDialog()
                {
                    Filter = "PNG Image | *.png",
                    DefaultExt = "png"
                };

                if (saveFileDialog.ShowDialog() == true)
                    bmp.Save(saveFileDialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        public Stream GetStream(System.Drawing.Image img, ImageFormat format)
        {
            var ms = new MemoryStream();
            img.Save(ms, format);
            return ms;
        }

        private async Task<LiveCameraResult> FacesAnalysisFunction(VideoFrame frame)
        {
            Console.WriteLine("rorororororor");
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var attrs = new List<FaceAttributeType> { FaceAttributeType.Age,
                FaceAttributeType.Gender, FaceAttributeType.HeadPose };
            var faces = await faceClient.DetectAsync(jpg, returnFaceAttributes: attrs);
            // Count the API call. 
            Console.WriteLine("!!!!"+faces.Length);
            // Output. 
            return new LiveCameraResult { Faces = faces };
        }

        private async void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (!string.IsNullOrWhiteSpace(OverlayImagePath))
            {
                var frame = eventArgs.Frame; // reference to the current frame   
               
                var g = Graphics.FromImage(frame);
                //  await faceClient.DetectAsync(frame.Image.ToMemoryStream(".jpg"));
                using (System.Drawing.Image backImage = (Bitmap)frame.Clone())
                using (System.Drawing.Image frontImage = System.Drawing.Image.FromFile(OverlayImagePath))
                using (System.Drawing.Image newImage = new Bitmap(backImage.Width, backImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    Stream faceimagestream = GetStream(newImage, System.Drawing.Imaging.ImageFormat.Jpeg);
                    try
                    {

                       // Console.WriteLine(faceimagestream);
                     //   Microsoft.ProjectOxford.Face.Contract.Face[] faces = await faceserviceclient.DetectAsync(faceimagestream);//, true, true,

                        //new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.HeadPose, FaceAttributeType.Glasses });
                      //  Console.WriteLine("asdasdasdasdasdasdasd");
                        //if (faces.Length >= 0)

                        //{

                        //  System.Console.WriteLine("There is no face in current frame");

                        // return;

                        //}
                    }
                    catch (Exception ex)
                    {

                        System.Console.WriteLine(ex);

                    }


                    using (Graphics compositeGraphics = Graphics.FromImage(newImage))
                    {
                        compositeGraphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                        compositeGraphics.DrawImageUnscaled(backImage, 0, 0);
                        compositeGraphics.DrawImageUnscaled(frontImage, 250, 350); // TODO: make positioning dynamic or configurable
                        compositeGraphics.Dispose();
                        frontImage.Dispose();
                        backImage.Dispose();
                    }

                    g.DrawImage(newImage, new PointF(0, 0));
                    g.Dispose();
                }
            }
        }
    }
}
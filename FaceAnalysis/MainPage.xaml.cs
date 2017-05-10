//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.FaceAnalysis;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace PhotoEditor
{
    /// <summary>
    /// Page for demonstrating FaceDetection on an image file.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        #region Globals

        private PackageCatalog packageCatalog;

        /// <summary>
        /// Brush for drawing the bounding box around each detected face.
        /// </summary>
        private readonly SolidColorBrush lineBrush = new SolidColorBrush(Windows.UI.Colors.Yellow);

        private IList<DetectedFace> faces = null;
        private WriteableBitmap displaySource = null;

        private const string OPT_PKG_LIB_FILE = @"OptionalPackageDLL.dll";
        private const string OPT_PKG_LIB_GETAGE_EXPORT = "GetAge";

        /// <summary>
        /// Thickness of the face bounding box lines.
        /// </summary>
        private readonly double lineThickness = 2.0;

        /// <summary>
        /// Transparent fill for the bounding box.
        /// </summary>
        private readonly SolidColorBrush fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);

        /// <summary>
        /// Limit on the height of the source image (in pixels) passed into FaceDetector for performance considerations.
        /// Images larger that this size will be downscaled proportionally.
        /// </summary>
        /// <remarks>
        /// This is an arbitrary value that was chosen for this scenario, in which FaceDetector performance isn't too important but face
        /// detection accuracy is; a generous size is used.
        /// Your application may have different performance and accuracy needs and you'll need to decide how best to control input.
        /// </remarks>
        private readonly uint sourceImageHeightLimit = 1280;

        #endregion

        /// <summary>
        /// Reference back to the "root" page of the app.
        /// </summary>
        private MainPage rootPage;

        /// <summary>
        /// Initializes a new instance of the <see cref="DetectFacesInPhoto" /> class.
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            ApplicationView.PreferredLaunchViewSize = new Size(1400, 850);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            Loaded += new RoutedEventHandler(page_Loaded);
        }

        public void page_Loaded(object sender, RoutedEventArgs e)
        {
            var currentAppPackage = Windows.ApplicationModel.Package.Current;
            foreach (var package in currentAppPackage.Dependencies)
            {
                if (package.IsOptional && package.Id.FamilyName.Contains("FabrikamFaceFilters"))
                {
                    FilterStackPanel.Visibility = Visibility.Visible;
                }
                else if(package.IsOptional && package.Id.FamilyName.Contains("FabrikamAgeAnalysis"))
                {
                    UtilityStackPanel.Visibility = Visibility.Visible;
                }
            }

            HookupCatalog();           
        }

        private void HookupCatalog()
        {
            try
            {
                packageCatalog = PackageCatalog.OpenForCurrentPackage();
                packageCatalog.PackageInstalling += Catalog_PackageInstalling;
            }
            catch (Exception ex)
            {
                PopupUI("Unable to setup deployment event handlers. {" + ex.InnerException + "}");
            }
        }

        private async void Catalog_PackageInstalling(PackageCatalog sender, PackageInstallingEventArgs args)
        {
            if (args.Progress == 100 && args.IsComplete
                && args.Package.IsOptional && args.Package.Id.FamilyName.Contains("FabrikamFaceFilters"))
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    FilterStackPanel.Visibility = Visibility.Visible;
                });
                
            }
            else if ((args.Progress == 100 && args.IsComplete
                && args.Package.IsOptional && args.Package.Id.FamilyName.Contains("FabrikamAgeAnalysis")))
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
               () =>
               {
                   UtilityStackPanel.Visibility = Visibility.Visible;
               });
                
            }
        }        

        /// <summary>
        /// Takes the photo image and FaceDetector results and assembles the visualization onto the Canvas.
        /// </summary>
        /// <param name="displaySource">Bitmap object holding the image we're going to display</param>
        /// <param name="foundFaces">List of detected faces; output from FaceDetector</param>
        private async void SetupVisualization(WriteableBitmap displaySource, IList<DetectedFace> foundFaces, string filter = null, bool AnalyzeAge = false)
        {
            ImageBrush brush = new ImageBrush();
            brush.ImageSource = displaySource;
            brush.Stretch = Stretch.Uniform;
            this.PhotoCanvas.Background = brush;

            if (foundFaces != null)
            {
                double widthScale = displaySource.PixelWidth / this.PhotoCanvas.ActualWidth;
                double heightScale = displaySource.PixelHeight / this.PhotoCanvas.ActualHeight;

                foreach (DetectedFace face in foundFaces)
                {
                    // Create a rectangle element for displaying the face box but since we're using a Canvas
                    // we must scale the rectangles according to the image’s actual size.
                    // The original FaceBox values are saved in the Rectangle's Tag field so we can update the
                    // boxes when the Canvas is resized.
                    Rectangle box = new Rectangle();
                    box.Tag = face.FaceBox;
                    box.Width = (uint)(face.FaceBox.Width / widthScale);
                    box.Height = (uint)(face.FaceBox.Height / heightScale);
                    box.Fill = this.fillBrush;
                    box.Stroke = this.lineBrush;
                    box.StrokeThickness = this.lineThickness;
                    box.Margin = new Thickness((uint)(face.FaceBox.X / widthScale), (uint)(face.FaceBox.Y / heightScale), 0, 0);
                    this.PhotoCanvas.Children.Add(box);

                    if (filter != null)
                    {
                        Rectangle filterPicture = new Rectangle();
                        filterPicture.Width = (uint)(face.FaceBox.Width / widthScale);
                        filterPicture.Height = (uint)(face.FaceBox.Height / heightScale);
                        
                        BitmapImage bit = new BitmapImage(new Uri(this.BaseUri, filter));
                        ImageBrush pic = new ImageBrush();
                        //pic.Stretch = Stretch.Fill;
                        pic.ImageSource = bit;
                        filterPicture.Fill = pic;
                        filterPicture.Margin = new Thickness((uint)(face.FaceBox.X / widthScale), (uint)(face.FaceBox.Y / heightScale), 0, 0);
                        this.PhotoCanvas.Children.Add(filterPicture);
                    }


                    // If optional package is installed
                    if (AnalyzeAge)
                    {
                        //var age = GetAge(face);
                        TextBlock text = new TextBlock();
                        text.Text = CalculateAge().ToString();
                        text.FontFamily = new FontFamily("Verdana");
                        text.FontSize = 28;
                        text.Foreground = new SolidColorBrush(Windows.UI.Colors.Yellow);
                        text.Width = (uint)(face.FaceBox.Width / widthScale);
                        text.Height = (uint)(face.FaceBox.Height / heightScale);
                        // Automatically adjust fot size to height
                        text.FontSize = (text.Height - 7) * 2.5 / 4;
                        text.Margin = new Thickness((uint)(face.FaceBox.X / widthScale), (uint)(face.FaceBox.Y / heightScale), 0, 0);
                        this.PhotoCanvas.Children.Add(text);
                    }
                }
            }

            string message;
            if (foundFaces == null || foundFaces.Count == 0)
            {
                message = "Didn't find any human faces in the image";
            }
            else if (foundFaces.Count == 1)
            {
                message = "Found a human face in the image";
            }
            else
            {
                message = "Found " + foundFaces.Count + " human faces in the image";
            }

            //this.rootPage.NotifyUser(message, NotifyType.StatusMessage);
        }     



        /// <summary>
        /// Clears the display of image and face boxes.
        /// </summary>
        private void ClearVisualization(bool ClearImage = true)
        {
            if (ClearImage)
            {
                this.PhotoCanvas.Background = null;
            }
            this.PhotoCanvas.Children.Clear();
            //this.rootPage.NotifyUser(string.Empty, NotifyType.StatusMessage);
        }

        /// <summary>
        /// Computes a BitmapTransform to downscale the source image if it's too large. 
        /// </summary>
        /// <remarks>
        /// Performance of the FaceDetector degrades significantly with large images, and in most cases it's best to downscale
        /// the source bitmaps if they're too large before passing them into FaceDetector. Remember through, your application's performance needs will vary.
        /// </remarks>
        /// <param name="sourceDecoder">Source image decoder</param>
        /// <returns>A BitmapTransform object holding scaling values if source image is too large</returns>
        private BitmapTransform ComputeScalingTransformForSourceImage(BitmapDecoder sourceDecoder)
        {
            BitmapTransform transform = new BitmapTransform();

            if (sourceDecoder.PixelHeight > this.sourceImageHeightLimit)
            {
                float scalingFactor = (float)this.sourceImageHeightLimit / (float)sourceDecoder.PixelHeight;

                transform.ScaledWidth = (uint)Math.Floor(sourceDecoder.PixelWidth * scalingFactor);
                transform.ScaledHeight = (uint)Math.Floor(sourceDecoder.PixelHeight * scalingFactor);
            }

            return transform;
        }

        /// <summary>
        /// Loads an image file (selected by the user) and runs the FaceDetector on the loaded bitmap. If successful calls SetupVisualization to display the results.
        /// </summary>
        /// <param name="sender">Button user clicked</param>
        /// <param name="e">Event data</param>
        private async void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            SoftwareBitmap detectorInput = null;

            try
            {
                FileOpenPicker photoPicker = new FileOpenPicker();
                photoPicker.ViewMode = PickerViewMode.Thumbnail;
                photoPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                photoPicker.FileTypeFilter.Add(".jpg");
                photoPicker.FileTypeFilter.Add(".jpeg");
                photoPicker.FileTypeFilter.Add(".png");
                photoPicker.FileTypeFilter.Add(".bmp");

                StorageFile photoFile = await photoPicker.PickSingleFileAsync();
                if (photoFile == null)
                {
                    return;
                }

                this.ClearVisualization();
                //this.rootPage.NotifyUser("Opening...", NotifyType.StatusMessage);

                // Open the image file and decode the bitmap into memory.
                // We'll need to make 2 bitmap copies: one for the FaceDetector and another to display.
                using (IRandomAccessStream fileStream = await photoFile.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);
                    BitmapTransform transform = this.ComputeScalingTransformForSourceImage(decoder);

                    using (SoftwareBitmap originalBitmap = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, BitmapAlphaMode.Ignore, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage))
                    {
                        // We need to convert the image into a format that's compatible with FaceDetector.
                        // Gray8 should be a good type but verify it against FaceDetector’s supported formats.
                        const BitmapPixelFormat InputPixelFormat = BitmapPixelFormat.Gray8;
                        if (FaceDetector.IsBitmapPixelFormatSupported(InputPixelFormat))
                        {
                            using (detectorInput = SoftwareBitmap.Convert(originalBitmap, InputPixelFormat))
                            {
                                // Create a WritableBitmap for our visualization display; copy the original bitmap pixels to wb's buffer.
                                this.displaySource = new WriteableBitmap(originalBitmap.PixelWidth, originalBitmap.PixelHeight);
                                originalBitmap.CopyToBuffer(displaySource.PixelBuffer);

                                //this.rootPage.NotifyUser("Detecting...", NotifyType.StatusMessage);

                                // Initialize our FaceDetector and execute it against our input image.
                                // NOTE: FaceDetector initialization can take a long time, and in most cases
                                // you should create a member variable and reuse the object.
                                // However, for simplicity in this scenario we instantiate a new instance each time.
                                FaceDetector detector = await FaceDetector.CreateAsync();
                                this.faces = await detector.DetectFacesAsync(detectorInput);

                                // Create our display using the available image and face results.
                                this.SetupVisualization(displaySource, faces);
                            }
                        }
                        else
                        {
                            //this.rootPage.NotifyUser("PixelFormat '" + InputPixelFormat.ToString() + "' is not supported by FaceDetector", NotifyType.ErrorMessage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.ClearVisualization();
                //this.rootPage.NotifyUser(ex.ToString(), NotifyType.ErrorMessage);
            }
        }

        /// <summary>
        /// Updates any existing face bounding boxes in response to changes in the size of the Canvas.
        /// </summary>
        /// <param name="sender">Canvas object whose size has changed</param>
        /// <param name="e">Event data</param>
        private void PhotoCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // If the Canvas is resized we must recompute a new scaling factor and
                // apply it to each face box.
                if (this.PhotoCanvas.Background != null)
                {
                    WriteableBitmap displaySource = (this.PhotoCanvas.Background as ImageBrush).ImageSource as WriteableBitmap;

                    double widthScale = displaySource.PixelWidth / this.PhotoCanvas.ActualWidth;
                    double heightScale = displaySource.PixelHeight / this.PhotoCanvas.ActualHeight;

                    foreach (var item in PhotoCanvas.Children)
                    {
                        Rectangle box = item as Rectangle;
                        if (box == null)
                        {
                            continue;
                        }

                        // We saved the original size of the face box in the rectangles Tag field.
                        BitmapBounds faceBounds = (BitmapBounds)box.Tag;
                        box.Width = (uint)(faceBounds.Width / widthScale);
                        box.Height = (uint)(faceBounds.Height / heightScale);

                        box.Margin = new Thickness((uint)(faceBounds.X / widthScale), (uint)(faceBounds.Y / heightScale), 0, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                //this.rootPage.NotifyUser(ex.ToString(), NotifyType.ErrorMessage);
            }
        }     

        private async void AddFilter_Click(object sender, RoutedEventArgs e)
        {
            //check if optional package is installed first
            var currentAppPackage = Windows.ApplicationModel.Package.Current;
            foreach(var package in currentAppPackage.Dependencies)
            {
                if(package.IsOptional && package.Id.FamilyName.Contains("FabrikamFaceFilters"))
                {
                    StorageFolder installFolder = package.InstalledLocation;
                    StorageFolder content = await installFolder.GetFolderAsync("Content");

                    // fetch filter name that was selected and visualize
                    string filterName = (e.OriginalSource as FrameworkElement).Name;
                    StorageFile file = await content.GetFileAsync(filterName + ".png");
                    this.SetupVisualization(displaySource, faces, file.Path);
                }
            }
        }
        
        delegate Int32 CodeDelegate();
        private int CalculateAge()
        {
            Int32 age = 0;
            var currentAppPackage = Windows.ApplicationModel.Package.Current;
            foreach (var package in currentAppPackage.Dependencies)
            {
                if (package.IsOptional && package.Id.FamilyName.Contains("FabrikamAgeAnalysis"))
                {
                    try
                    {
                        IntPtr handle = LoadPackagedLibrary(OPT_PKG_LIB_FILE);
                        if (handle == IntPtr.Zero)
                        {
                            PopupUI("Failed to load dll");
                        }
                        else
                        {
                            IntPtr FuncPTR = GetProcAddress(handle, OPT_PKG_LIB_GETAGE_EXPORT);
                            if (FuncPTR != IntPtr.Zero)
                            {
                                CodeDelegate ageFunction = Marshal.GetDelegateForFunctionPointer<CodeDelegate>(FuncPTR);
                                age = ageFunction();                                
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PopupUI("Exception thrown while loading code {" + ex.InnerException + "}");
                    }
                }
            }
            return age;
        }

        private async void PopupUI(string text)
        {
            await new MessageDialog(text).ShowAsync();
        }

        #region Interop
        [DllImport("kernel32", EntryPoint = "LoadPackagedLibrary", SetLastError = true)]
        static extern IntPtr LoadPackagedLibrary([MarshalAs(UnmanagedType.LPWStr)] string lpFileName, int reserved = 0);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        #endregion
        private void GuessAge_Click(object sender, RoutedEventArgs e)
        {
            this.ClearVisualization();
            this.SetupVisualization(displaySource, faces, null, true);
        }

        

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            this.ClearVisualization(false);
        }
    }
}

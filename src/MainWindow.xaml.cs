using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Kinect;
using System.Windows.Media.Animation;

namespace K4W.KinectTelevision
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// The KinectSensor used in this application
        /// </summary>
        private KinectSensor _currentSensor = null;

        /// <summary>
        /// Used as the source of the Image-control to display the video stream
        /// </summary>
        private WriteableBitmap _outputBitmap = null;

        /// <summary>
        /// Data from a ColorImageFrame
        /// </summary>
        private byte[] _pixelData = new byte[0];


        public MainWindow()
        {
            InitializeComponent();

            // Load formats
            LoadFormats();

            // Get first running Kinect sensor
            KinectSensor runningSensor = KinectSensor.KinectSensors.FirstOrDefault(sens => sens.Status == KinectStatus.Connected);

            // Start Kinect
            StartSensor(runningSensor);

            // Listen to StatusChanged-event
            KinectSensor.KinectSensors.StatusChanged += OnKinectSensorChanged;

            // Listen to closing-event
            this.Closing += OnClosing;
        }


        /// <summary>
        /// Handles status changes for a sensor
        /// </summary>
        private void OnKinectSensorChanged(object sender, StatusChangedEventArgs e)
        {
            if (e.Sensor == _currentSensor)
            {
                if (e.Status != KinectStatus.Connected)
                {
                    StartSensor(null);
                }
                else StartSensor(e.Sensor);
            }
            else if ((_currentSensor == null) && (e.Sensor.Status == KinectStatus.Connected))
                StartSensor(e.Sensor);
        }

        /// <summary>
        /// Starts a new sensor after the old one has stoppped. If 'sensor' is null a static image will be shown.
        /// </summary>
        /// <param name="sensor">New instance of the sensor</param>
        private void StartSensor(KinectSensor sensor)
        {
            // Stop if still running
            if (_currentSensor != null)
                _currentSensor.Stop();

            // Get currently selected format
            ColorImageFormat selectedFormat = (ColorImageFormat)FormatComboBox.SelectedItem;

            if (sensor == null)
            {
                // Hide output window
                Output.Visibility = Visibility.Collapsed;

                // Save 'null'
                _currentSensor = null;

                return;
            }

            // Save into global
            _currentSensor = sensor;
            
            if (selectedFormat == ColorImageFormat.Undefined)
            {
                // Hide output window
                Output.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Show output window
                Output.Visibility = Visibility.Visible;

                // Enable color-stream
                _currentSensor.ColorStream.Enable(selectedFormat);

                // Listen to colorFrameReady-event
                _currentSensor.ColorFrameReady += OnColorFrameReadyHandler;

                // Start the sensor
                _currentSensor.Start();

                // Set basic angle
                SetBasicElevationLevel();
            }
        }

        /// <summary>
        /// Get the elevation level of the sensor and update the slider
        /// </summary>
        private void SetBasicElevationLevel()
        {
            if (_currentSensor != null)
                ElevationSlider.Value = _currentSensor.ElevationAngle;
        }

        /// <summary>
        ///  Load all image formats into the combobox
        /// </summary>
        private void LoadFormats()
        {
            IList<ColorImageFormat> colorImageFormat = new List<ColorImageFormat> 
                                                        {
                                                            ColorImageFormat.RgbResolution640x480Fps30,
                                                            ColorImageFormat.RgbResolution1280x960Fps12,
                                                            ColorImageFormat.YuvResolution640x480Fps15,
                                                            ColorImageFormat.InfraredResolution640x480Fps30,
                                                            ColorImageFormat.Undefined
                                                        };

            FormatComboBox.ItemsSource = colorImageFormat;
            FormatComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Processes the incoming ColorImageFrame from the Kinect Sensor
        /// </summary>
        private void OnColorFrameReadyHandler(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                // The frame will be null if we are too late to process it
                if (colorFrame == null)
                    return;

                // Checks if the length has changed (means the format has changed)
                if (_pixelData.Length != colorFrame.PixelDataLength)
                {
                    // Creates a buffer long enough to receive all the data of a frame
                    _pixelData = new byte[colorFrame.PixelDataLength];

                    /* Most of the formats provided by the kinect are 4-bytes per pixel
                       * The infrared stream only has 2 bytes so we'll have to check that*/
                    PixelFormat currentFormat = _currentSensor.ColorStream.FrameBytesPerPixel == 4
                                                ? PixelFormats.Bgr32
                                                : PixelFormats.Gray16;

                    /* Use a WriteableBitmap because it's better to re-write some pixels
                     * of a WritebaleBitmap than creating a new BitmapImage */
                    _outputBitmap = new WriteableBitmap(colorFrame.Width,
                                                      colorFrame.Height,

                                                      // Standard DPI
                                                      96, 96,

                                                      // Current format for the ColorImageFormat
                                                      currentFormat,

                                                      // BitmapPalette
                                                      null);

                    // Assign the writeablebitmap to the imagecontrol
                    Output.Source = _outputBitmap;
                }

                // Copies the data from the frame to the pixelData array
                colorFrame.CopyPixelDataTo(_pixelData);

                // Update the writeable bitmap
                _outputBitmap.WritePixels(
                    // Represents the size of our image
                   new Int32Rect(0, 0, colorFrame.Width, colorFrame.Height),
                    // Our image data
                   _pixelData,
                    // How much bytes are there in a single row?
                   colorFrame.Width * colorFrame.BytesPerPixel,
                    // Offset for the buffer, where does he need to start
                   0);
            }
        }

        /// <summary>
        /// Get the value from the slider and change the elevation angle from the sensor
        /// </summary>
        private void OnChangeElevationClick(object sender, RoutedEventArgs e)
        {
            if (_currentSensor != null)
                _currentSensor.ElevationAngle = (int)ElevationSlider.Value;
        }

        /// <summary>
        /// Handles the SelectionChanged event of the FormatComboBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Controls.SelectionChangedEventArgs"/> instance containing the event data.</param>
        private void OnSelectedFormatChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentSensor != null && _currentSensor.Status == KinectStatus.Connected && e.AddedItems.Count > 0)
            {
                // Disable the color stream for a second
                _currentSensor.ColorStream.Disable();

                // Retrieve the selected format
                ColorImageFormat selectedFormat = (ColorImageFormat)e.AddedItems[0];

                // Check if the ColorImageFormat is different from Undefined, if so, show that it is unsupported
                if (selectedFormat == ColorImageFormat.Undefined)
                {
                    Output.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Output.Visibility = Visibility.Visible;

                    // Enable the stream with the new stream
                    _currentSensor.ColorStream.Enable(selectedFormat);
                }
            }
        }

        /// <summary>
        /// Stop the Kinect when closing the app
        /// </summary>
        void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_currentSensor != null && _currentSensor.Status == KinectStatus.Connected)
                _currentSensor.Stop();
        }

        #region UI Properties / Methods / Events

        private bool _isPopupVisible = false;

        private void OnToggleSettingsClick(object sender, RoutedEventArgs e)
        {
            if (_isPopupVisible == true)
            {
                BeginStoryboard((Storyboard)FindResource("HidePopup"));
                _isPopupVisible = false;
            }
            else
            {
                BeginStoryboard((Storyboard)FindResource("ShowPopup"));
                _isPopupVisible = true;
            }
        }

        #endregion

    }
}

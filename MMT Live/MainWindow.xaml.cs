using System.Collections.Concurrent;
using System.Threading;


namespace VisioForge_MMT
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    using VisioForge.VideoFingerPrinting;
    using VisioForge.VideoFingerPrinting.Sources;
    using VisioForge.VideoFingerPrinting.Sources.DirectShow;
    using VisioForge.VideoFingerPrinting.Sources.MFP;

    using VisioForge_MMT.Classes;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : IDisposable
    {
        private FingerprintLiveData _searchLiveData;

        private FingerprintLiveData _searchLiveOverlapData;

        private ConcurrentQueue<FingerprintLiveData> _fingerprintQueue;

        private IntPtr _tempBuffer;

        private List<VFPFingerPrint> _adVFPList;

        private List<DetectedAd> _results;

        private List<VFRect> _ignoredAreas;

        private long _fragmentDuration;

        private int _fragmentCount;

        private int _overlapFragmentCount;

        private object _processingLock;

        private SimpleCapture _videoCapture;

        private SimplePlayer _videoPlayer;

        public MainWindow()
        {
            InitializeComponent();

            _videoPlayer = new SimplePlayer(null);
            _videoPlayer.OnVideoFrame += VideoCapture1_OnVideoFrameBuffer;
            _videoPlayer.OnError += VideoCapture1_OnError;

            _videoCapture = new SimpleCapture(null);
            _videoCapture.OnVideoFrame += VideoCapture1_OnVideoFrameBuffer;
            _videoCapture.OnError += VideoCapture1_OnError;
        }

        private void btAddAdFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = Settings.LastPath
            };

            System.Windows.Forms.DialogResult result = dlg.ShowDialog(this.GetIWin32Window());

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                var files = FileScanner.SearchVideoInFolder(dlg.SelectedPath);
                foreach (var file in files)
                {
                    lbAdFiles.Items.Add(file);
                }
                
                Settings.LastPath = dlg.SelectedPath;
            }
        }

        private void btClearAds_Click(object sender, RoutedEventArgs e)
        {
            lbAdFiles.Items.Clear();
        }

        private void btStart_Click(object sender, RoutedEventArgs e)
        {
            if ((string)btStart.Content == "Stop")
            {
                _videoCapture?.Stop();
                _videoPlayer?.Stop();

                Thread.Sleep(500);

                ProcessVideoDelegateMethod();

                btStart.Content = "Start";

                lbStatus.Content = string.Empty;

                if (_tempBuffer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(_tempBuffer);
                    _tempBuffer = IntPtr.Zero;
                }

                pnScreen.BeginInit();
                pnScreen.Source = null;
                pnScreen.EndInit();
            }
            else
            {
                btStart.IsEnabled = false;
                
                lbStatus.Content = "Step 1: Searching video files";
                
                _fragmentCount = 0;
                _overlapFragmentCount = 0;

                var engine = VFSimplePlayerEngine.LAV;

                switch (cbEngine.SelectedIndex)
                {
                    case 0:
                        engine = VFSimplePlayerEngine.DirectShow;
                        break;
                    case 1:
                        engine = VFSimplePlayerEngine.FFMPEG;
                        break;
                    case 2:
                        engine = VFSimplePlayerEngine.LAV;
                        break;
                }

                var adList = new List<string>();

                _adVFPList = new List<VFPFingerPrint>();
                
                foreach (string item in lbAdFiles.Items)
                {
                    adList.Add(item);
                }

                lbStatus.Content = "Step 2: Getting fingerprints for ads files";

                if (adList.Count == 0)
                {
                    btStart.Content = "Start";
                    lbStatus.Content = string.Empty;

                    MessageBox.Show("Ads list is empty!");
                    
                    return;
                }

                int progress = 0;
                foreach (string filename in adList)
                {
                    pbProgress.Value = progress;
                    string error = "";
                    VFPFingerPrint fp;

                    if (File.Exists(filename + ".vfsigx"))
                    {
                        fp = VFPFingerPrint.Load(filename + ".vfsigx");
                    }
                    else
                    {
                        var source = new VFPFingerprintSource(filename, engine);
                        foreach (var area in _ignoredAreas)
                        {
                            source.IgnoredAreas.Add(area);
                        }

                        fp = VFPAnalyzer.GetSearchFingerprintForVideoFile(source, out error);
                    }
                    
                    if (fp == null)
                    {
                        MessageBox.Show("Unable to get fingerpring for video file: " + filename + ". Error: " + error);
                    }
                    else
                    {
                        fp.Save(filename + ".vfsigx", false);
                        _adVFPList.Add(fp);
                    }

                    progress += 100 / adList.Count;
                }

                int fragmentDurationProperty = Convert.ToInt32(edFragmentDuration.Text);
                if (fragmentDurationProperty != 0)
                {
                    _fragmentDuration = fragmentDurationProperty * 1000;
                }
                else
                {
                    var maxDuration = _adVFPList.Max((print => print.Duration));
                    long minfragmentDuration = (((maxDuration + 1000) / 1000) + 1) * 1000;
                    _fragmentDuration = minfragmentDuration * 2;
                }

                pbProgress.Value = 100;
                
                if (_tempBuffer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(_tempBuffer);
                    _tempBuffer = IntPtr.Zero;
                }

                lbStatus.Content = "Step 3: Starting video preview";

                if (cbSource.SelectedIndex == 0)
                {
                    _videoCapture.Video_CaptureDevice_Name = cbVideoSource.Text;
                    _videoCapture.Video_CaptureDevice_Format = cbVideoFormat.Text;
                    _videoCapture.Video_CaptureDevice_FrameRate = Convert.ToDouble(cbVideoFrameRate.Text);

                    _videoCapture.Start();
                }
                else
                {
                    string url = edNetworkSourceURL.Text;
                    //var ip = new IPCameraSourceSettings
                    //             {
                    //                 URL =,
                    //                 Login = edNetworkSourceLogin.Text,
                    //                 Password = edNetworkSourcePassword.Text
                    //             };
                    _videoPlayer.Filename = url;
                    
                    switch (cbNetworkSourceEngine.SelectedIndex)
                    {
                        case 0:
                            _videoPlayer.Engine = VFSimplePlayerEngine.LAV;
                            break;
                        case 2:
                            _videoPlayer.Engine = VFSimplePlayerEngine.FFMPEG;
                            break;
                    }

                    _videoPlayer.Start();
                }

                lbStatus.Content = "Step 4: Getting data";

                pbProgress.Value = 0;

                lvResults.Items.Refresh();

                btStart.IsEnabled = true;
                btStart.Content = "Stop";
            }
        }

        #region List view

        private ObservableCollection<ResultsViewModel> resultsView = new ObservableCollection<ResultsViewModel>();

        public ObservableCollection<ResultsViewModel> ResultsView
        {
            get
            {
                return resultsView;
            }
        }

        #endregion

        private void btSaveResults_Click(object sender, RoutedEventArgs e)
        {
            string xml = XmlUtility.Obj2XmlStr(resultsView);

            var dlg = new System.Windows.Forms.SaveFileDialog
            {
                Filter = @"XML file|*.xml"
            };

            var result = dlg.ShowDialog(this.GetIWin32Window());

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string filename = dlg.FileName;

                if (File.Exists(filename))
                {
                    File.Delete(filename);
                }

                if (!File.Exists(filename))
                {
                    // Create a file to write to.
                    using (StreamWriter sw = File.CreateText(filename))
                    {
                        sw.WriteLine(xml);
                    }
                }
            }
        }

        private void SaveSettings()
        {
            string filename = Settings.SettingsFolder + "settings.xml";

            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            if (!Directory.Exists(Settings.SettingsFolder))
            {
                Directory.CreateDirectory(Settings.SettingsFolder);
            }

            Settings.Save(typeof(Settings), filename);
        }

        private void LoadSettings()
        {
            string filename = Settings.SettingsFolder + "settings.xml";

            if (File.Exists(filename))
            {
                Settings.Load(typeof(Settings), filename);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();

            foreach (var item in _videoCapture.Video_CaptureDevices)
            {
                cbVideoSource.Items.Add(item);
            }

            if (cbVideoSource.Items.Count > 0)
            {
                cbVideoSource.SelectedIndex = 0;
                cbVideoSource_SelectionChanged(null, null);
            }

            _fingerprintQueue = new ConcurrentQueue<FingerprintLiveData>();

            _processingLock = new object();
            _results = new List<DetectedAd>();
            _ignoredAreas = new List<VFRect>();
        }

        private void VideoCapture1_OnError(object sender, ErrorsEventArgs e)
        {
            edLog.Text += e.Message + Environment.NewLine;
        }

        [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, int length);

        private delegate void ProcessVideoDelegate();

        private void ProcessVideoDelegateMethod()
        {
            lock (_processingLock)
            {
                //if (VideoCapture1.Status == VFVideoCaptureStatus.Free)
                //{
                //    return;
                //}

                //// done. searching for fingerprints.
                //VideoCapture1.Stop();

                long n;
                FingerprintLiveData fingerprint = null;

                if (_fingerprintQueue.TryDequeue(out fingerprint))
                {
                    IntPtr p = VFPSearch.Build(out n, ref fingerprint.Data);

                    VFPFingerPrint fvp = new VFPFingerPrint()
                    {
                        Data = new byte[n],
                        OriginalFilename = string.Empty
                    };

                    Marshal.Copy(p, fvp.Data, 0, (int) n);

                    foreach (var ad in _adVFPList)
                    {
                        List<int> positions;
                        bool found = VFPAnalyzer.Search(ad, fvp, ad.Duration, (int)slDifference.Value, out positions, true);

                        if (found)
                        {
                            foreach (var pos in positions)
                            {
                                DateTime tm = fingerprint.StartTime.AddMilliseconds(pos * 1000);

                                bool duplicate = false;
                                foreach (var detectedAd in _results)
                                {
                                    long time = 0;

                                    if (detectedAd.Timestamp > tm)
                                    {
                                        time = (long)(detectedAd.Timestamp - tm).TotalMilliseconds;
                                    }
                                    else
                                    {
                                        time = (long)(tm - detectedAd.Timestamp).TotalMilliseconds;
                                    }

                                    if (time < 1000)
                                    {
                                        // duplicate
                                        duplicate = true;
                                        break;
                                    }
                                }

                                if (duplicate)
                                {
                                    break;
                                }

                                _results.Add(new DetectedAd(tm));
                                resultsView.Add(
                                    new ResultsViewModel()
                                    {
                                        Sample = ad.OriginalFilename,
                                        TimeStamp = tm.ToString("HH:mm:ss.fff"),
                                        TimeStampMS = tm - new DateTime(1970, 1, 1)
                                    });
                            }
                        }
                    }

                    fingerprint.Data.Free();
                    fingerprint.Dispose();
                }
            }
        }

        private WriteableBitmap _frameBitmap;

        private bool _frameStopped;

        private delegate void NewFrameDelegate(SampleGrabberBufferCBEventArgs e);

        private void NewFrameDelegateMethod(SampleGrabberBufferCBEventArgs e)
        {
            try
            {
                if (pnScreen == null)
                {
                    return;
                }

                if (_frameStopped)
                {
                    return;
                }

                if (_frameBitmap == null || _frameBitmap.PixelWidth != e.Width || _frameBitmap.PixelHeight != e.Height)
                {
                    _frameBitmap = new WriteableBitmap(e.Width, e.Height, 72, 72, PixelFormats.Bgr24, null);

                    pnScreen.BeginInit();
                    pnScreen.Source = _frameBitmap;
                    pnScreen.EndInit();
                }

                pnScreen.BeginInit();
                int lineStep = (((e.Width * 24) + 31) / 32) * 4;
                _frameBitmap.WritePixels(new Int32Rect(0, 0, e.Width, e.Height), e.Buffer, (int)e.BufferLen, lineStep);
                pnScreen.EndInit();
            }
            catch
            {
            }
        }


        private void VideoCapture1_OnVideoFrameBuffer(object sender, SampleGrabberBufferCBEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new NewFrameDelegate(NewFrameDelegateMethod), e);
                
                if (_tempBuffer == IntPtr.Zero)
                {
                    _tempBuffer = Marshal.AllocCoTaskMem(ImageHelper.GetStrideRGB24(e.Width) * e.Height);
                }

                // live
                if (_searchLiveData == null)
                {
                    _searchLiveData = new FingerprintLiveData((int)(_fragmentDuration / 1000), DateTime.Now);
                    _fragmentCount++;
                }

                long timestamp = (long)(e.SampleTime * 1000);
                if (timestamp < _fragmentDuration * _fragmentCount)
                {
                    ImageHelper.CopyMemory(_tempBuffer, e.Buffer, e.BufferLen);

                    // process frame to remove ignored areas
                    if (_ignoredAreas.Count > 0)
                    {
                        foreach (var area in _ignoredAreas)
                        {
                            if (area.Right > e.Width || area.Bottom > e.Height)
                            {
                                continue;
                            }

                            MFP.FillColor(_tempBuffer, e.Width, e.Height, area, 0);
                        }
                    }

                    VFPSearch.Process(_tempBuffer, e.Width, e.Height, ImageHelper.GetStrideRGB24(e.Width), timestamp, ref _searchLiveData.Data);
                }
                else
                {
                    _fingerprintQueue.Enqueue(_searchLiveData);

                    _searchLiveData = null;

                    Dispatcher.BeginInvoke(new ProcessVideoDelegate(ProcessVideoDelegateMethod));
                }

                // overlap
                if (timestamp < _fragmentDuration / 2)
                {
                    return;
                }

                if (_searchLiveOverlapData == null)
                {
                    _searchLiveOverlapData = new FingerprintLiveData((int)(_fragmentDuration / 1000), DateTime.Now);
                    _overlapFragmentCount++;
                }

                if (timestamp < _fragmentDuration * _overlapFragmentCount + _fragmentDuration / 2)
                {
                    ImageHelper.CopyMemory(_tempBuffer, e.Buffer, e.BufferLen);
                    VFPSearch.Process(_tempBuffer, e.Width, e.Height, ImageHelper.GetStrideRGB24(e.Width), timestamp, ref _searchLiveOverlapData.Data);
                }
                else
                {
                    _fingerprintQueue.Enqueue(_searchLiveOverlapData);

                    _searchLiveOverlapData = null;

                    Dispatcher.BeginInvoke(new ProcessVideoDelegate(ProcessVideoDelegateMethod));
                }
            }
            catch
            {
            }
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void cbVideoSource_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            string val = e.AddedItems[0].ToString();
            if (string.IsNullOrEmpty(val))
            {
                return;
            }

            _videoCapture.Video_CaptureDevice_Name = val;
            _videoCapture.Video_CaptureDevice_ReadFormats();

            // enumerate video formats
            cbVideoFormat.Items.Clear();

            foreach (var format in _videoCapture.Video_CaptureDevice_Formats)
            {
                cbVideoFormat.Items.Add(format);
            }

            if (cbVideoFormat.Items.Count > 0)
            {
                cbVideoFormat.SelectedIndex = 0;
            }

            // enumerate video frame rates
            cbVideoFrameRate.Items.Clear();

            foreach (var frameRate in _videoCapture.Video_CaptureDevice_FrameRates)
            {
                cbVideoFrameRate.Items.Add(frameRate);
            }

            if (cbVideoFrameRate.Items.Count > 0)
            {
                cbVideoFrameRate.SelectedIndex = 0;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
        }

        #region Dispose

        /// <summary>
        /// Dispose flag.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Finalizes an instance of the <see cref="MainWindow"/> class. 
        /// </summary>
        ~MainWindow()
        {
            Dispose(false);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">
        /// Disposing parameter.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects).
                }

                // Free your own state (unmanaged objects).
                // Set large fields to null.

                if (_searchLiveData != null)
                {
                    _searchLiveData.Dispose();
                    _searchLiveData = null;
                }

                if (_searchLiveOverlapData != null)
                {
                    _searchLiveOverlapData.Dispose();
                    _searchLiveOverlapData = null;
                }

                disposed = true;
            }
        }

        #endregion

        private void btIgnoredAreaAdd_Click(object sender, RoutedEventArgs e)
        {
            var rect = new VFRect()
            {
                Left = Convert.ToInt32(edIgnoredAreaLeft.Text),
                Top = Convert.ToInt32(edIgnoredAreaTop.Text),
                Right = Convert.ToInt32(edIgnoredAreaRight.Text),
                Bottom = Convert.ToInt32(edIgnoredAreaBottom.Text)
            };

            _ignoredAreas.Add(rect);
            lbIgnoredAreas.Items.Add($"Left: {rect.Left}, Top: {rect.Top}, Right: {rect.Right}, Bottom: {rect.Bottom}");
        }

        private void btIgnoredAreasRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            int index = lbIgnoredAreas.SelectedIndex;
            if (index >= 0)
            {
                lbIgnoredAreas.Items.RemoveAt(index);
                _ignoredAreas.RemoveAt(index);
            }
        }

        private void btIgnoredAreasRemoveAll_Click(object sender, RoutedEventArgs e)
        {
            lbIgnoredAreas.Items.Clear();
            _ignoredAreas.Clear();
        }

        private void btSortResults_Click(object sender, RoutedEventArgs e)
        {
            resultsView = new ObservableCollection<ResultsViewModel>(resultsView.OrderBy(i => i.TimeStampMS.TotalMilliseconds));
            lvResults.ItemsSource = resultsView;
        }

        private void BtAddAdFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.OpenFileDialog
            {
                InitialDirectory = Settings.LastPath,
                Multiselect = true
            };

            System.Windows.Forms.DialogResult result = dlg.ShowDialog(this.GetIWin32Window());

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                foreach (var name in dlg.FileNames)
                {
                    this.lbAdFiles.Items.Add(name);
                }

                Settings.LastPath = Path.GetFullPath(dlg.FileNames[0]);
            }
        }
    }
}

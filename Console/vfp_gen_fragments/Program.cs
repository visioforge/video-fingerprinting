using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VisioForge.VideoFingerPrinting;
using VisioForge.VideoFingerPrinting.Sources;
using VisioForge.VideoFingerPrinting.Sources.DirectShow;
using VisioForge.VideoFingerPrinting.Sources.MediaInfo;
using VisioForge.VideoFingerPrinting.Sources.MFP;

namespace vfp_gen_fragments
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new CommandLineOptions();
            if (!CommandLine.Parser.Default.ParseArguments(args, options))
            {
                // Display the default usage information
                //Console.WriteLine(options.GetUsage());
                return;
            }

            if (!File.Exists(options.InputFile))
            {
                Console.WriteLine("Source file not found: " + options.InputFile + ".");
                return;
            }

            VFPAnalyzer.SetLicenseKey(options.LicenseKey);

            var engine = VFSimplePlayerEngine.LAV;

            if (!string.IsNullOrEmpty(options.Engine))
            {
                switch (options.Engine.Trim())
                {
                    case "directshow":
                        engine = VFSimplePlayerEngine.DirectShow;
                        break;
                    case "ffmpeg":
                        engine = VFSimplePlayerEngine.FFMPEG;
                        break;
                    case "lav":
                        engine = VFSimplePlayerEngine.LAV;
                        break;
                }
            }

            Console.WriteLine("Analyzing...");

            var time = DateTime.Now;
            var mediaPlayer = new SimplePlayer(null)
            {
                Filename = options.InputFile,
                Video_Renderer = VFVideoRendererInternal.None
            };

            if (Path.GetExtension(options.InputFile)?.ToLowerInvariant() == ".wmv")
            {
                engine = VFSimplePlayerEngine.DirectShow;
            }

            mediaPlayer.Engine = engine;
            mediaPlayer.MaximalSpeedPlayback = true;

            //if (!string.IsNullOrEmpty(DebugDir))
            //{
            //    mediaPlayer.Debug_Dir = DebugDir;
            //    mediaPlayer.Debug_Mode = true;
            //}

            var mediaInfo = new MediaInfoReader
            {
                Filename = options.InputFile
            };
            mediaInfo.ReadFileInfo(true);


            VFPSearchData[] dataList = new VFPSearchData[options.Count];

            string error1 = string.Empty;
            bool inUse = true;
            bool error = false;

            // check for hang or something
            long frameNumber = 0;

            int sourceWidth = 0;
            int sourceHeight = 0;

            int index = 0;
            long minusTime = 0;
            mediaPlayer.OnVideoFrame += delegate (object sender, SampleGrabberBufferCBEventArgs e)
            {
                if (!inUse)
                {
                    return;
                }

                long timestamp = (long)(e.SampleTime * 1000);
                timestamp -= minusTime;

                if (timestamp >= options.Duration)
                {
                    Console.WriteLine($"Started new fragment at {minusTime}");
                    index++;
                    minusTime = (long)(e.SampleTime * 1000);
                    timestamp -= minusTime;
                }

                if (index >= options.Count)
                {
                    inUse = false;
                    return;
                }

                if (e.SampleTime < options.Duration * options.Count)
                {
                    frameNumber++;

                    sourceWidth = e.Width;
                    sourceHeight = e.Height;

                    if (dataList[index] == null)
                    {
                        dataList[index] = new VFPSearchData(options.Duration / 1000);
                    }

                    VFPSearch.Process(
                        e.Buffer,
                        e.Width,
                        e.Height,
                        ImageHelper.GetStrideRGB24(e.Width),
                        timestamp,
                        ref dataList[index]);
                }
                else
                {
                    inUse = false;
                }
            };

            mediaPlayer.OnStop += delegate
            {
                inUse = false;
            };

            mediaPlayer.OnError += delegate (object sender, ErrorsEventArgs e)
            {
                if (e.Message.Contains("FULL"))
                {
                    return;
                }

                if (e.Level == DebugLevel.Error)
                {
                    error = true;
                    inUse = false;

                    error1 = e.Message + " | " + e.CallSite;
                }
            };

            mediaPlayer.Start();

            while (inUse && !error)
            {
                Thread.Sleep(100);
                Application.DoEvents();
            }

            mediaPlayer.Stop();

            Debug.WriteLine($"Processed {frameNumber} frame for file {options.InputFile}.");

            if (!error)
            {
                for (var i = 0; i < dataList.Length; i++)
                {
                    var data = dataList[i];
                    if (data == null)
                    {
                        break;
                    }

                    IntPtr p = VFPSearch.Build(out var n, ref data);

                    VFPFingerPrint fvp = new VFPFingerPrint()
                    {
                        // ReSharper disable once ExceptionNotDocumented
                        Data = new byte[n],
                        OriginalFilename = options.InputFile,
                        OriginalDuration = options.Duration,
                        Duration = options.Duration,
                        ID = Guid.NewGuid(),
                        Width = sourceWidth,
                        Height = sourceHeight,
                        FrameRate = mediaInfo.Video_FrameRate(0)
                    };

                    Marshal.Copy(p, fvp.Data, 0, (int) n);

                    data.Free();

                    var newFilename = $"{options.InputFile}_segment{i}.vfsigx";
                    fvp.Save(newFilename, false);
                }
            }
            else
            {
                Debug.WriteLine($"Error: {error1}.");
            }

            mediaPlayer.Dispose();

            var elapsed = DateTime.Now - time;
            Console.WriteLine("Analyze finished. Elapsed time: " + elapsed.ToString("g"));
        }
    }
}

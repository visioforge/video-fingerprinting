using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vfpgen
{
    using System.IO;

    using VisioForge.VideoFingerPrinting;
    using VisioForge.VideoFingerPrinting.Sources;

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

            if (File.Exists(options.OutputFile))
            {
                try
                {
                    File.Delete(options.OutputFile);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to delete output file: " + options.OutputFile + ". Exception: " + e.Message + ".");
                    return;
                }
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

            Console.WriteLine("Starting analyze.");

            var time = DateTime.Now;

            var source = new VFPFingerprintSource(options.InputFile, engine);
            if (options.Duration > 0)
            {
                source.StopTime = TimeSpan.FromMilliseconds(options.Duration);
            }

            VFPFingerPrint fp;
            if (options.Type == "search")
            {
                fp = VFPAnalyzer.GetSearchFingerprintForVideoFile(source, ErrorCallback);
            }
            else
            {
                fp = VFPAnalyzer.GetComparingFingerprintForVideoFile(source, ErrorCallback);
            }

            if (fp == null)
            {
                Console.WriteLine("Unable to get fingerprint.");
                return;
            }

            var elapsed = DateTime.Now - time;
            Console.WriteLine("Analyze finished. Elapsed time: " + elapsed.ToString("g"));

            fp.Save(options.OutputFile);
        }

        private static void ErrorCallback(string error)
        {
            Console.WriteLine(error);
        }
    }
}

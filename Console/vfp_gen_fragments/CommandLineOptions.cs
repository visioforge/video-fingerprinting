using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vfp_gen_fragments
{
    using CommandLine;
    using CommandLine.Text;

    public class CommandLineOptions
    {
        [Option('i', "input", Required = true, HelpText = "Input video file.")]
        public string InputFile { get; set; }

        [Option('e', "engine", Required = false, HelpText = "Engine to use. Possible values: directshow, ffmpeg, lav.", DefaultValue = "lav")]
        public string Engine { get; set; }

        [Option('t', "type", Required = false, HelpText = "Fingerprint type, for search or for compare. Possible values: search, compare.", DefaultValue = "search")]
        public string Type { get; set; }

        [Option('d', "duration", Required = false, HelpText = "Duration of fragment to analyze, in milliseconds.", DefaultValue = 3000)]
        public int Duration { get; set; }

        [Option('c', "count", Required = false, HelpText = "Count of fragments to analyze.", DefaultValue = 10)]
        public int Count { get; set; }

        [Option('l', "license", Required = false, HelpText = "License key.", DefaultValue = "TRIAL")]
        public string LicenseKey { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var help = new HelpText
            {
                Heading = new HeadingInfo("VisioForge Video Fingerprinting SDK signatures generator (several fragments)", "10.0"),
                Copyright = new CopyrightInfo("VisioForge", 2020),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };

            help.AddPreOptionsLine("Usage: app -i \"input file\" options");
            help.AddOptions(this);

            return help;
        }
    }
}

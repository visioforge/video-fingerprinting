﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vfpgen
{
    using CommandLine;
    using CommandLine.Text;

    public class CommandLineOptions
    {
        [Option('i', "input", Required = true, HelpText = "Input video file.")]
        public string InputFile { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output signature file.")]
        public string OutputFile { get; set; }

        [Option('e', "engine", Required = false, HelpText = "Engine to use. Possible values: directshow, ffmpeg, lav.", DefaultValue = "lav")]
        public string Engine { get; set; }

        [Option('t', "type", Required = false, HelpText = "Fingerprint type, for search or for compare. Possible values: search, compare.", DefaultValue = "search")]
        public string Type { get; set; }

        [Option('d', "duration", Required = false, HelpText = "Duration of fragment to analyze, in milliseconds. 0 to analyze full file.", DefaultValue = 0)]
        public int Duration { get; set; }

        [Option('l', "license", Required = false, HelpText = "License key.", DefaultValue = "TRIAL")]
        public string LicenseKey { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var help = new HelpText
            {
                Heading = new HeadingInfo("VisioForge Video Fingerprinting SDK signatures generator", "10.0"),
                Copyright = new CopyrightInfo("VisioForge", 2020),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };

            help.AddPreOptionsLine("Usage: app -i \"input file\" -o \"output file\" options");
            help.AddOptions(this);

            return help;
        }
    }
}

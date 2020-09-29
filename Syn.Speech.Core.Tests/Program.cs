using Syn.Speech.Api;
using Syn.Speech.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace Syn.Speech.Core.Tests
{
    class Program
    {
        private static Configuration _configuration;
        private static StreamSpeechRecognizer _recognizer;

        static void Main(string[] args)
        {
            InitializeEngine();
        }

        static void InitializeEngine()
        {
            Console.WriteLine(@"Started...");
            Logger.LogReceived += Logger_LogReceived;

            _configuration = new Configuration
            {
                AcousticModelPath = ("Models"),
                DictionaryPath = ("Models/cmudict-en-us.dict"),
                //LanguageModelPath = ("Models/en-us.lm.dmp"),
            };

            _configuration.UseGrammar = true;
            _configuration.GrammarPath = "Models";
            _configuration.GrammarName = "Hello";

            _recognizer = new StreamSpeechRecognizer(_configuration);

            StartRecognition();
        }

        static void StartRecognition()
        {
            var audioDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Audio");
            var audioCollection = new Dictionary<string, FileInfo>();

            foreach (var item in Directory.GetFiles(audioDirectory))
            {
                var audioFile = new FileInfo(item);
                audioCollection.Add(audioFile.Name, audioFile);
            }

            var audioListString = string.Join(" \n", audioCollection.Keys);

            Console.WriteLine(@"Which file to use -> {0}", audioListString);
            var response = Console.ReadLine();
            if (string.IsNullOrEmpty(response)) response = "robot.wav";

            _recognizer.StartRecognition(new FileStream(Path.Combine(audioDirectory, response), FileMode.Open));

            Console.WriteLine(@"Press any key to start Speech Recognition...");
            Console.ReadLine();

            var result = _recognizer.GetResult();
            _recognizer.StopRecognition();
            if (result != null)
            {
                Console.WriteLine(result.GetHypothesis());
            }
        }

        static void Logger_LogReceived(object sender, LogReceivedEventArgs e)
        {
            Console.WriteLine(e.Message);
        }
    }
}

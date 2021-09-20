﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

using LogfileMetaAnalyser.Helpers;
using LogfileMetaAnalyser.Datastore;
using LogfileMetaAnalyser.LogReader;


namespace LogfileMetaAnalyser
{
    public class Analyzer
    {
        public event EventHandler<double> OnReadProgressChanged;


        private int _AnalyzeDOP;
        private ILogReader m_LogReader;

        public int AnalyzeDOP
        {
            get { return _AnalyzeDOP; }
            set { _AnalyzeDOP = Math.Max(1, Math.Min(value, Environment.ProcessorCount - 1)); }
        }
        private Helpers.NLog logger;

        public DatastoreStructure datastore = new DatastoreStructure(); 

        
        //Constructor
        public Analyzer()
        {
            logger = new Helpers.NLog("Analyzer");
            logger.Info("Starting analyzer");

            AnalyzeDOP = 2; 
        }

        public void Initialize(ILogReader reader)
        {
            m_LogReader = reader;
        }

        //main procedure to analyze a log file
        public async Task AnalyzeStructureAsync()
        {
            //clear old datastore values
            datastore.Clear();

            if (m_LogReader == null)
                return;

            logger.Info($"Starting analyzing {m_LogReader.Display ?? "?"}.");

            List<Detectors.ILogDetector> detectors = new List<Detectors.ILogDetector>
            {
                new Detectors.TimeRangeDetector(),
                new Detectors.TimeGapDetector(),
                new Detectors.SyncStructureDetector(),
                new Detectors.ConnectorsDetector(),
                new Detectors.SyncStepDetailsDetector(),
                new Detectors.SQLStatementDetector(),
                new Detectors.JobServiceJobsDetector(),
                new Detectors.SyncJournalDetector()
            };
            

            //Auslesen der Required Parent Detectors und zuweisen der resourcen
            foreach (var childDetector in detectors.Where(d => d.requiredParentDetectors.Any()))
            {
                List<Detectors.ILogDetector> listOfParentDetectors = new List<Detectors.ILogDetector>();
                foreach (var parentId in childDetector.requiredParentDetectors)
                {
                    var parentDetector = detectors.FirstOrDefault(d => d.identifier == parentId);
                    if (parentDetector != null)
                        listOfParentDetectors.Add(parentDetector);
                }
                childDetector.parentDetectors = listOfParentDetectors.ToArray();
            }


            //detector inits
            foreach (var detector in detectors)
            {
                detector.datastore = datastore;
                detector.InitializeDetector();
                detector.isEnabled = true;
            }


            //text reading
            logger.Info("Starting reading the text");
            GlobalStopWatch.StartWatch("TextReading");
            await TextReading(m_LogReader, detectors).ConfigureAwait(false);

            GlobalStopWatch.StopWatch("TextReading");


            //detecor finilize; call the finalize in that order, that "child" detectors's finalize is called after the parent's finalize is done
            logger.Info("Starting to perform finalizedDetector");
            GlobalStopWatch.StartWatch("finalizedDetector");
            List<string> finalizedDetectorIds = new List<string>();
            while (1 == 1)
            {
                var detectorsToFinalize = detectors.Where(d => d.requiredParentDetectors.Length == 0 ||  //the ones which are parents and not child detectors
                                                               d.requiredParentDetectors.Any(f => finalizedDetectorIds.Any(kk => kk == f)))  //all child detectors for which the parent is already finalized prior
                                                    .Where(d => !finalizedDetectorIds.Any(kk => kk == d.identifier))  //exclude already finalized detecrtors
                                                    .ToArray();
                if (detectorsToFinalize.Length == 0)
                    break;

                foreach (var detector in detectorsToFinalize)
                {
                    detector.FinalizeDetector();
                    finalizedDetectorIds.Add(detector.identifier);
                }
            }
            GlobalStopWatch.StopWatch("finalizedDetector");

#if DEBUG
            var stopwatchresults = GlobalStopWatch.GetResult();
#endif
            logger.Info("Analyzer done!");
        }

        private async Task TextReading(ILogReader logReader, List<Detectors.ILogDetector> detectors)
        {
            if (detectors == null)
                return;

            logger.Info($"Starting reading {logReader.GetType().Name}");

            // TODO respect Constants.NumberOfContextMessages

            await foreach (var entry in logReader.ReadAsync())
            {
                Parallel.ForEach(detectors, new ParallelOptions {MaxDegreeOfParallelism = AnalyzeDOP},
                    detector =>
                    {
                        detector.ProcessMessage(new TextMessage(entry));
                    });
            }


            /*
            var parseStatisticPerTextfile = new List<ParseStatistic>();
             
            TextMessage msg;

            int curFileNr = 0;
            foreach (string filename in filesToRead)
            {
                logger.Info($"Starting reading file {filename}");

                curFileNr++;
                float percentDone = (100f * (curFileNr - 1) / filesToRead.Length);

                ParseStatistic parseStatistic = new ParseStatistic() { filename = filename, filesizeKb = (FileHelper.GetFileSizes(new string[] { filename }) / 1024f).Int() };

                Stopwatch sw_reading = new Stopwatch();
                Stopwatch sw_overall = new Stopwatch();
                sw_overall.Start();
                
                using (var reader = new ReadLogByBlock(filename, Constants.messageInsignificantStopTermRegexString, Constants.NumberOfContextMessages))
                {
                    //refire the progress event
                    if (OnReadProgressChanged != null)
                        reader.OnProgressChanged += new EventHandler<ReadLogByBlockEventArgs>((object o, ReadLogByBlockEventArgs args) =>
                        {
                            OnReadProgressChanged(this, (args.progressPercent / filesToRead.Length) + percentDone);
                        });
                    
                    //read the messages from this log                    
                    while (reader.HasMoreMessages)
                    {
                        sw_reading.Start();
                        GlobalStopWatch.StartWatch("TextReading.GetNextMessageAsync()");
                        msg = await reader.GetNextMessageAsync().ConfigureAwait(false);
                        sw_reading.Stop();
                        GlobalStopWatch.StopWatch("TextReading.GetNextMessageAsync()");

                        if (msg == null)
                            break;


                        //pass the message to all detectors                        
                        //skip invalid messages that would confuse the detectors
                        GlobalStopWatch.StartWatch("TextReading.ProcessMessage()");
                        if (msg.numberOfLines > 0 && msg.textLocator.fileLinePosition > 0)
                            Parallel.ForEach(detectors, new ParallelOptions() { MaxDegreeOfParallelism = AnalyzeDOP }, (detector) =>
                            {
                                detector.ProcessMessage(msg);
                            });
                        GlobalStopWatch.StopWatch("TextReading.ProcessMessage()");
                    }

                    //refire the progress event - this file is 100% done
                    OnReadProgressChanged?.Invoke(this, (100f / filesToRead.Length) + percentDone);
                }

                sw_reading.Stop();
                sw_overall.Stop();

                parseStatistic.readAndParseFileDuration = sw_reading.ElapsedMilliseconds;
                parseStatistic.overallDuration = sw_overall.ElapsedMilliseconds;

                parseStatisticPerTextfile.Add(parseStatistic);
            }

            datastore.statistics.parseStatistic.AddRange(parseStatisticPerTextfile);
            */
        } 
        
    }
}

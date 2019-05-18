﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using GraphSynth.Representation;
using GraphSynth.Search.Algorithms;
using GraphSynth.Search.Tools;
using OpenBabelFunctions;





namespace GraphSynth.Search
{
    public class RandomRuleApplication: SearchProcess
    {
        
        private readonly string _runDirectory;
        private readonly string _inputFilePath;
        
        private candidate Seed;
        private JobBuffer jobBuffer;
        private const int NUM_TRAIL = 22;
        private const int TOTAL_RULE = 5;
        
        private static Mutex mutex = new Mutex();


        /// <inheritdoc />
        /// <summary>
        /// Initializes SearchProcess properties.
        /// </summary>
        public RandomRuleApplication(GlobalSettings settings): base(settings) 
        {
            RequireSeed = true;
            RequiredNumRuleSets = 1;
            AutoPlay = true;
            
            _runDirectory = Path.Combine(settings.OutputDirAbs, "RandomRuleApplication", "randomCarbox");
            if (Directory.Exists(_runDirectory))
                Directory.Delete(_runDirectory, true);
            Directory.CreateDirectory(_runDirectory);

            Seed = new candidate(OBFunctions.tagconvexhullpoints(settings.seed), settings.numOfRuleSets);
            jobBuffer = new JobBuffer(_runDirectory);
        }

        protected override void Run()
        {
            Thread generateLinkers = new Thread(Generate);
            Thread autoReleaseBuffer = new Thread(AutoSubmitSimulation);
            
            generateLinkers.Start();
            autoReleaseBuffer.Start();


            generateLinkers.Join();
            autoReleaseBuffer.Join();

        }
        
        private void AutoSubmitSimulation()
        {
            var all_submitted = false;
            var buffer_empty = false;
            while (true)
            {
               //mutex.WaitOne();
                if (jobBuffer.BufferNotEmpty())
                {
                    jobBuffer.Check_finised();
                    if (!all_submitted && jobBuffer.SimQNotFull())
                    {
                        all_submitted = jobBuffer.Simulate();
                    }
                }
                else
                {
                    if (all_submitted)
                        break;
                }


               //mutex.ReleaseMutex();
            }
        }

        private void Generate()
        {
            Console.WriteLine(NUM_TRAIL);
            var linkerSet = new HashSet<string>();
            var agent = new Algorithms.Random(settings);
            for (var t = 0; t < NUM_TRAIL; t++)
            {
                Console.WriteLine("Trail: {0}", t);
                var cand = Seed.copy();

                for (var step = 1; step < TOTAL_RULE + 1; step++)
                {
                    var opt = agent.ChooseOption(cand);
                    if (opt == null)
                        return;
                    agent.ApplyOption(opt, cand, true);
                }
                var carboxOpt = agent.ChooseCarboxOption(cand);
                if (carboxOpt == null)
                    return;
                agent.ApplyOption(carboxOpt, cand, true);
                var candSMILE = OBFunctions.moltoSMILES(OBFunctions.designgraphtomol(cand.graph));
                var linkerName = AbstractAlgorithm.GetLinkerName(cand);
                Console.WriteLine(candSMILE);
                Console.WriteLine(linkerName);
                if (linkerSet.Contains(linkerName))
                {
                    t--;
                    continue;
                }
                linkerSet.Add(linkerName);
                var coeff = Path.Combine(_runDirectory, "linker" + linkerName + ".coeff");
                var lmpdat = Path.Combine(_runDirectory, "linker" + linkerName + ".lmpdat");
                agent.Converter.moltoUFF(OBFunctions.designgraphtomol(cand.graph), coeff, lmpdat, false, 100);

                //mutex.WaitOne();
                jobBuffer.Add(linkerName, AbstractAlgorithm.Rand.NextDouble());
                //mutex.ReleaseMutex();
            }
            jobBuffer.Add("finish", 2.0);
            
            
        }
        
        public override string text => "RandomTrail Search Runner";
        
    }
}
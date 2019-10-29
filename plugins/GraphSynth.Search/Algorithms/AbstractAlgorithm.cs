using GraphSynth.Representation;
using OpenBabel;
using OpenBabelFunctions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using library;



namespace GraphSynth.Search.Algorithms {
    
    public abstract class AbstractAlgorithm {
        public static GlobalSettings Settings;
        public static readonly System.Random Rand = new System.Random();
        //private static readonly string IODir = OBFunctions.GetRamDir();
        
        private const double AngleFloor = 155; // minimum acceptable angle between carboxylates
        
        
        protected AbstractAlgorithm(GlobalSettings settings_) {
            Settings = settings_;
        }

        public static string GetLinkerName(candidate cand)
        {
            var arr = cand.recipe.Select(x => Convert.ToString(x.optionNumber));
            return String.Join("-", arr);
        }
        
        public static int CountAtoms(candidate cand)
        {
            return cand.graph.nodes.Count;
        }


        /// <summary>
        /// Get all available options for the given graph.
        /// </summary>
        /// <param name="cand"></param>
        /// <returns></returns>
        public static List<option> GetNoneTerminalOptions(candidate cand) {
            var options = new List<option>();
            options.AddRange(Settings.rulesets[0].recognize(cand.graph, false));
            return options;
        }
        

        /// <summary>
        /// Returns the option corresponding to the carboxyl rule application.
        /// </summary>
        /// <param name="cand"></param>
        /// <returns></returns>
        public static List<option> GetTerminalOptions(candidate cand) {
            var options = new List<option>();
            options.AddRange(Settings.rulesets[1].recognize(cand.graph, false));
            return options;
        }

        /// <summary>
        /// Apply the option to the candidate and store the agent's evaluation.
        /// </summary>
        public void ApplyOption(option opt, candidate cand, bool doMinimize) {
            cand.graph.globalVariables.Add(cand.f0); // track fitness values of previous states
            opt.apply(cand.graph, null);
            cand.addToRecipe(opt);
//            if(doMinimize)
//                cand.graph = Minimize(cand.graph);
            cand.graph = OBFunctions.tagconvexhullpoints(cand.graph);
        }


        /// <summary>
        /// Copies the candidate graph, transfers the L-mapping, and returns the resultant candidate.
        /// </summary>
        public candidate CopyAndApplyOption(option opt, candidate cand, bool doMinimize) {
            var newCand = cand.copy();
            var newOpt = opt.copy();
            SearchProcess.transferLmappingToChild(newCand.graph, cand.graph, newOpt);
            ApplyOption(newOpt, newCand, doMinimize);
            return newCand;
        }
        
        /// <summary>
        /// Clean way to minimize a graph.
        /// </summary>
        private designGraph Minimize(designGraph graph) {
//            var mol = QuickMinimization(OBFunctions.designgraphtomol(graph), IODir + "rank" + ".lmpdat",
//                IODir + "rank" + ".coeff", false, 0);
//            OBFunctions.updatepositions(graph, mol);
            return graph;
        }

        private OBMol QuickMinimization(OBMol mol, string coeff, string lmpdat, bool periodic, int rankMe) {
            double padding = 50;
            const double etol = 0.0;
            const double ftol = 1.0e-6;
            const int maxiter = 40000;
            const int maxeval = 20000;
            
            var minSettings = new lammps.LAMMPSsettings();
            if (periodic) {
                minSettings.boundary = "p p p";
                padding = 0;
            }

            if (File.Exists(coeff))
                File.Delete(coeff);
            if (File.Exists(lmpdat))
                File.Delete(lmpdat);
            
            string[] lmparg = {"", "-screen", "none", "-log", "log.lammps." + rankMe};
            using (var lmps = new lammps(minSettings, lmparg)) {
                lmps.runCommand("read_data " + lmpdat);
                lmps.openFile(coeff);
                lmps.minimize(etol, ftol, maxiter, maxeval, "cg");
                OBFunctions.updatexyz(mol, lmps.getAtomPos());
            }
            return mol;
        }
    }
}
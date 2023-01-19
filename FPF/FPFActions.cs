using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Globalization;
using System.Text.RegularExpressions;
using ResultReader;

namespace FPF
{
    public class FPFActions
    {

        //Data storage
        private ds_Parameters paramsObj;
        private ds_DataContainer dataContainerObj;
        private ds_Filters filtersObj;
        private ds_Norm normObj;
        private ds_FilteredOutPsms filteredOutPsmsObj;

        //For log file and console log
        private List<string> logFileLines; //Content of log file
        private string logFile; // Output name for log file
        int notSingleHitCnt = 0; // Number of PSMs that has more than one first-rank hit
        int noSpstFeatCnt = 0; //Number of PSMs that are considered but with missing SpectraST feature values
        int consideredCnt = 0;// Number of PSMs that are considered by FPF
        int filteredOutCnt = 0;// Number of PSM that are filtered out by FPF

        /// <summary>
        /// Defines thread actions by specified id. 0 for reading the iProphet file from IDS
        /// and 1 for reading the iProphet file from ICS.
        /// </summary>
        public void DoWorkerJobs(int id)
        {
            switch (id)
            {
                case 0:
                    this.ReadIdsIpro();
                    break;
                case 1:
                    this.ReadIcsIpro();
                    break;
            }
        }

        /// <summary>
        /// Performs main actions.
        /// 1. Reads Parameter File
        /// 2. Reads the iProphet file from IDS with result reader and collect PSM IDs
        /// 3. Reads the iProphet file from ICS with result reader and collect PSM information
        /// 4. Calculates intra-peptide and intra-protein euclidean distance for each PSM in the iProphet file from ICS
        /// 5. Parses the iProphet file from ICS again, filter and write to new file
        /// </summary>
        /// <param name="paramFile">Parameter file name</param>
        ///
        public void MainActions(string paramFile)
        {
            //Initialization
            this.dataContainerObj = new ds_DataContainer();
            this.filtersObj = new ds_Filters();
            this.paramsObj = new ds_Parameters();
            this.normObj = new ds_Norm();
            this.filteredOutPsmsObj = new ds_FilteredOutPsms(filtersObj.featNum);
            this.logFileLines = new List<string>() {"Warning: PSM with more than one hit:"};

            //Read parameters file
            if (!File.Exists(paramFile))
                throw new FileNotFoundException("Parameter file not found!");
            this.ReadParamFile(Path.Combine(Directory.GetCurrentDirectory(),paramFile));

            //Read the two iProphet files simultaneously with two threads
            List<int> workerIds = new List<int>{0,1};
            Parallel.ForEach(workerIds, workerId => {
                this.DoWorkerJobs(workerId);
            });

            //Collect PSM information, calculate distances and filter
            this.CollectPsmFF_FromProtein_Dic();
            this.CalOverallEuDist();
            this.FilterIcsIproFile();

            //Write result to filteredoutFile
            if (this.filteredOutPsmsObj.FilteredOutFile != "")
                this.filteredOutPsmsObj.FilteredOutPsmsToFile(this.paramsObj.MainDir);

            //Write to log file and console
            logFile = GetLogFileName();
            File.WriteAllLines(Path.Combine(this.paramsObj.MainDir, logFile), logFileLines);
            Console.WriteLine("-------------------");
            Console.WriteLine(String.Format("FPF actions done! Examined: {0} PSMs, filtered out: {1} PSMs", consideredCnt, filteredOutCnt));
            this.filteredOutPsmsObj.MeetingCritNumPsmCntToConosle();
            return;       
        }

        /// <summary>
        /// Reads the parameter file line by line and checks whether the parameter name corresponds to the correct parameter names.
        /// If the name is correct, modifies global variables.
        /// </summary>
        private void ReadParamFile(string paramFile)
        {
            Console.WriteLine("Reading parameter file...");
            string line;
            using StreamReader paramFileReader = new StreamReader(paramFile);
            while ((line = paramFileReader.ReadLine()) != null)
            {
                //Skip annotations or empty lines
                if (line == "")
                    continue;
                else if (line[0] == '#')
                    continue;

                String[] lineElementsArr = line.Split(new[] { ':' },2);
                lineElementsArr[0] = lineElementsArr[0].ToLower().Trim(); //parameter name is not case-sensitive
                lineElementsArr[0] = Regex.Replace(lineElementsArr[0], @"\s+", " ");
                lineElementsArr[1] = lineElementsArr[1].Trim();

                //Check if the parameter or feature is already specified in another line and if the name is correct or not
                string errorCode = String.Format("Error: some parameter has been modified to \"{0}\"", lineElementsArr[0]);
                if (paramsObj.ValidateParamName(lineElementsArr[0])) //If the line specifies a parameter
                {
                    if (paramsObj.GetParamIsSet(lineElementsArr[0]) == true) //Check whether the param is specified by the user already
                    {
                        errorCode = String.Format("Error: the parameter \"{0}\" has been repeatedly specified", lineElementsArr[0]);
                        throw new ApplicationException(errorCode);
                    }
                    paramsObj.SetParamAsTrue(lineElementsArr[0]);

                }
                else if (filtersObj.ValidateFeatureName(lineElementsArr[0])) //If the line specifies a filter
                {
                    if (filtersObj.GetFeatureIsSet(lineElementsArr[0]) == true) //Check whether the param is specified by the user already
                    {
                        errorCode = String.Format("Error: filters of the feature \"{0}\" has been repeatedly specified", lineElementsArr[0]);
                        throw new ApplicationException(errorCode);
                    }
                    filtersObj.SetFeatureAsTrue(lineElementsArr[0]);
                }
                else if (lineElementsArr[1] == "") //Check if the parameter value is specified as empty
                    continue;
                else //parameter name error
                    throw new ApplicationException(errorCode);

                //Set param or filter values
                switch (lineElementsArr[0])
                {
                    case "main directory":
                        this.paramsObj.MainDir = lineElementsArr[1];
                        break;
                    case "iprophet file from identification based on database searching (ids)":
                        this.paramsObj.IdsIproFile = lineElementsArr[1];
                        break;
                    case "iprophet file from identification based on combined database and spectral library searching (ics)":
                        this.paramsObj.IcsIproFile = lineElementsArr[1];
                        break;
                    case "output iprophet file":
                        this.paramsObj.ModIcsIproFile = lineElementsArr[1];
                        break;
                    case "output csv file":
                        this.filteredOutPsmsObj.FilteredOutFile =  lineElementsArr[1];
                        break;
                    case "reference channel":
                        if (int.TryParse(lineElementsArr[1], out int refChannel) == false)
                            throw new ApplicationException (String.Format ("Error: incorrect format for \"reference channel\""));
                        else if (refChannel <= 0)
                            throw new ApplicationException (String.Format ("Error: reference channel number out of range!"));
                        this.paramsObj.RefChannel = refChannel;
                        break;
                    case "decoy prefixes or suffixes":
                        if (lineElementsArr[1].ToLower() != "none")
                        {
                            string[] decoyKeywordArr = lineElementsArr[1].Split(',').Select(decoyPrefix => decoyPrefix.Trim()).ToArray();
                            this.paramsObj.AddDecoyKeyword(decoyKeywordArr);
                        }
                        break;
                    case "proteins to be excluded from normalization":
                        if (lineElementsArr[1].ToLower() != "none")
                        {
                            string[] bgKeywordArr = lineElementsArr[1].Split(',').Select(decoyPrefix => decoyPrefix.Trim()).ToArray();
                            this.normObj.AddStdProtKeyword(bgKeywordArr); //Add background keywords to the object
                        }
                        break;
                    default: //Add feature
                        this.filtersObj.AddFilters(lineElementsArr[0], lineElementsArr[1]);
                        break;
                }
            }
            //Check if all the parameters are specified by the user (no need to check filter)
            List<string> missingParams = paramsObj.CheckAllParamsSet();
            if (missingParams.Count > 0) //Some of the parameters are missing
            {
                string errorcode = "Error: the values of the following parameters are not specified:\n";
                foreach (string missingParam in missingParams)
                    errorcode += String.Format("\"{0}\"\n", missingParam);
                throw new ApplicationException(errorcode);
            }


            paramFileReader.Close();
            return;
        }

        /// <summary>
        /// Only valid PSMs should be considered when filtering.
        /// Check whether the PSM is valid (not decoy prot, without shared peptide, with probability passing FDR) and whether the PSM contains zero reporter ion intensity.
        /// </summary>
        /// <param name="fdr001Prob">FDR 1% probability for the iProphet file</param>
        /// <param name="decoyPrefixLi">List that contains all the possible decoy prefixes and suffixes</param>
        /// <returns>-1 for invalid PSMs, 0 for valid PSMs but with missing reporter ion intensity, and 1 for valid PSMs without missing reporter ion intensity.</returns>
        public static int PsmIsValid(ds_PSM psm, ds_Peptide pep, ds_Protein prot, float fdr001Prob, List<(string, string)> decoyKeywordLi)
        {      
            //Check if the protein is decoy
            foreach ((string, string) decoyKeyword in decoyKeywordLi)
            {
                if (decoyKeyword.Item1 == "PRE" && prot.ProtID.StartsWith(decoyKeyword.Item2) || decoyKeyword.Item1 == "SUF" && prot.ProtID.EndsWith(decoyKeyword.Item2))
                    return -1;
            }

            //Check if the peptide is shared
            if (pep.b_IsUnique == false)
                return -1;

            //Check if the PSM has interprophet probability < 1% FDR probability
            string keyScoreType = "peptideprophet_result";
            Dictionary<string, double> psmScoreDic = (Dictionary<string, double>) psm.Score;
            double psmScore;
            if (psmScoreDic.ContainsKey(keyScoreType))
                psmScore = psmScoreDic[keyScoreType];
            else //search hits with no iProphet score
                return -1;
            if (psmScore < fdr001Prob)
                return -1;
            
            //Check if the PSM has any missing intensity value
            List<double> psmIntenLi = new List<double>();
            psmIntenLi.AddRange(psm.libra_ChanIntenDi.Values);
            if (psmIntenLi.Contains(0))
                return 0;   
            
            return 1;
        }

        /// <summary>
        /// 1. Parses PSMs in the iProphet file from IDS with result reader.
        /// 2. Adds PSMs which are valid to the list.
        /// 3. Calculates normalization ratio using the iProphet file from IDS.
        /// </summary>
        private void ReadIdsIpro()
        {
            Console.WriteLine("Parsing the iProphet file from IDS...");
            PepXmlProtXmlReader idsIproReader = new PepXmlProtXmlReader();
            if (!File.Exists(Path.Combine(this.paramsObj.MainDir, this.paramsObj.IdsIproFile)))
                throw new FileNotFoundException(String.Format("iProphet file from IDS \"{0}\" not found!", Path.Combine(this.paramsObj.MainDir, this.paramsObj.IdsIproFile)));
            this.dataContainerObj.idsIproResult = idsIproReader.ReadFiles(Path.Combine(this.paramsObj.MainDir, this.paramsObj.IdsIproFile), "",
                XmlParser_Action.Read_PepXml, SearchResult_Source.TPP_PepXml);

            //get FDR 1% probability
            this.paramsObj.IdsFdr001Prob = dataContainerObj.idsIproResult.GetPepMinProbForFDR(0.01f, "");

            //Check PSM validity
            foreach (KeyValuePair<string, ds_Protein> prot in this.dataContainerObj.idsIproResult.Protein_Dic)
            {
                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        //If PSM is valid, add PSM name to the list
                        if (PsmIsValid(psm, pep.Value, prot.Value, this.paramsObj.IdsFdr001Prob, this.paramsObj.DecoyKeywordLi) != -1 
                            && !this.dataContainerObj.idsPsmIdLi.Contains(psm.QueryNumber))
                            this.dataContainerObj.idsPsmIdLi.Add(psm.QueryNumber);
                    }
                }
            }

            //Get the number of channels
            this.paramsObj.ChannelCnt = this.dataContainerObj.idsIproResult.Protein_Dic.First<KeyValuePair<string, ds_Protein>>().Value.Peptide_Dic.First<KeyValuePair<string, ds_Peptide>>().Value.PsmList[0].libra_ChanIntenDi.Count<KeyValuePair<int, double>>();
            //Check whether reference channel is within the valid range
            if (this.paramsObj.RefChannel > this.paramsObj.ChannelCnt)
                throw new ApplicationException(String.Format("Error: reference channel number out of range!"));

            //Calculate ratio for normalization
            this.normObj.GetChannelMed(this.paramsObj, this.dataContainerObj.idsIproResult);

            //Print IDS PSM count
            Console.WriteLine(String.Format("Number of Valid PSMs in the iProphet file from IDS: {0}", this.dataContainerObj.idsPsmIdLi.Count()));
            return;
        }

        /// <summary>
        /// Parses iProphet file from ICS with result reader.
        /// </summary>
        private void ReadIcsIpro()
        {
            Console.WriteLine("Parsing the iProphet file from ICS...");
            PepXmlProtXmlReader icsIproReader = new PepXmlProtXmlReader();
            if (!File.Exists(Path.Combine(this.paramsObj.MainDir, this.paramsObj.IcsIproFile)))
                throw new FileNotFoundException(String.Format("iProphet file from ICS \"{0}\" not found!", Path.Combine(this.paramsObj.MainDir, this.paramsObj.IcsIproFile)));
            this.dataContainerObj.icsIproResult = icsIproReader.ReadFiles(Path.Combine(this.paramsObj.MainDir,this.paramsObj.IcsIproFile), "",
                XmlParser_Action.Read_PepXml, SearchResult_Source.TPP_PepXml);

            //get fdr < 1% probability
            this.paramsObj.IcsFdr001Prob = this.dataContainerObj.icsIproResult.GetPepMinProbForFDR(0.01f, "");
        }

        /// <summary>
        /// Collects feature values of valid PSMs in the iProphet file from ICS to icsPsmFFDic.
        /// </summary>
        private void CollectPsmFF_FromProtein_Dic()
        {
            Console.Write("Collecting PSM information");
            int psmCnt = 0; //For console
            foreach (KeyValuePair<string, ds_Protein> prot in this.dataContainerObj.icsIproResult.Protein_Dic)
            {
                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        int int_isValid = PsmIsValid(psm, pep.Value, prot.Value, this.paramsObj.IcsFdr001Prob, this.paramsObj.DecoyKeywordLi);
                        if (int_isValid == -1) //Invalid PSM
                            continue;

                        psmCnt++;
                        Dictionary<string, double> psmScoreDic = (Dictionary<string, double>)psm.Score;
                        Dictionary<string, double> spstScoreDic = new Dictionary<string, double>(); //Store scores specific for SpectraST
                        List<string> spstScoreNames = new List<string> { "dot", "delta", "precursor_mz_diff", "hits_num",
                            "hits_mean", "hits_stdev", "fval"}; //Features of SpectraST
                        foreach (string spstScoreName in spstScoreNames)
                        {
                            if (psmScoreDic.ContainsKey(spstScoreName)) //PSMs with SpectraST features
                                spstScoreDic.Add(spstScoreName, Math.Abs(psmScoreDic[spstScoreName]));
                            else //PSM without SpectraST features
                                spstScoreDic.Add(spstScoreName, -10000);
                        }

                        ds_Psm_ForFilter psmInfoObj = new ds_Psm_ForFilter(
                            prot.Key, //Protein
                            psm.Pep_exp_mass, //Mass
                            psm.Charge, //Charge
                            pep.Value.Sequence.Length, //Peptide length
                            pep.Value.ModPosList.Count, //PTM count
                            (double)pep.Value.ModPosList.Count / pep.Value.Sequence.Length, //PTM ratio
                            Math.Abs(psm.MassError), //Absolute Mass Difference
                            spstScoreDic["precursor_mz_diff"], //Precursor Mz Difference
                            spstScoreDic["dot"], //Dot Product
                            spstScoreDic["delta"], //Delta Score
                            spstScoreDic["hits_num"], //Hit Num
                            spstScoreDic["hits_mean"], //Hit Mean
                            spstScoreDic["hits_stdev"], //Hit Standard Deviation
                            spstScoreDic["fval"] //F-value
                            ) ;

                        if (int_isValid != 0) //Without missing intensity
                        {
                            List<double> psmNormIntenLi = this.normObj.GetNormIntenLi(this.paramsObj, psm.libra_ChanIntenDi.Values.ToList());
                            psmInfoObj.AvgInten = Math.Round(psmNormIntenLi.Average(),4);
                        }

                        //Add information to icsPsmFFDic if the current hit is the only hit for this PSM.
                        //If there is more than one hit, merely add the PSM name to log file and use the first hit.
                        try
                        {
                            this.dataContainerObj.icsPsmFFDic.Add(psm.QueryNumber, psmInfoObj);
                        }
                        catch
                        {
                            this.logFileLines.Add(psm.QueryNumber);
                            this.notSingleHitCnt++;
                        }

                        //Print to console
                        if (psmCnt % 10000 == 0)
                            Console.Write(psmCnt);
                        else if (psmCnt % 1000 == 0)
                            Console.Write(".");
                    }
                }
            }
            Console.Write("\n");
            Console.WriteLine(String.Format("Number of Valid PSMs in the iProphet file from ICS: {0}", this.dataContainerObj.icsPsmFFDic.Count()));
            //Write the total count of PSMs with more than one hit into log file
            this.logFileLines.Add(String.Format("{0} at total.", this.notSingleHitCnt));
        }

        /// <summary>
        /// Calculates ratio of each channel for a PSM and returns a list of ratios
        /// </summary>
        private List<double> CalRatio(List<double> intenLi)
        {
            List<double> ratioLi = new List<double>();
            for (int i = 0; i < this.paramsObj.ChannelCnt; i++)
            {
                if (i + 1 == this.paramsObj.RefChannel) //skip reference channel
                    continue;
                double ratio = intenLi[i] / intenLi[this.paramsObj.RefChannel - 1];
                ratioLi.Add(Math.Round(ratio, 4));
            }
            return ratioLi;
        }

        /// <summary>
        /// Given channel ratios from a set of PSMs (can be from a peptide or protein), then return a list containing the euclidean distance of each PSM
        /// Euclidean distance is the distance between the PSM and the average of other n-1 PSM ratios in the set.
        /// </summary>
        /// <param name="psmsRatioLi">An array containing ratios from a set of PSMs. Each row is a PSM.</param>
        /// <returns>A list containing euclidean distances of all PSMs from the input set</returns>
        private List<double> GetEuDistFromRatio(List<List<double>> psmsRatioLi, List<double> psmsTotalRatioLi)
        {
            List<double> euDistLi = new List<double>();
            if (psmsRatioLi.Count == 1) //If there is only one PSM in the set, set euclidean distance to 0
            {
                euDistLi.Add(0.0);
                return euDistLi;
            }

            //Store average ratio for each channel for all PSMs in protein
            List<double> avgRatioLi = new List<double>();
            for (int i = 0; i < paramsObj.ChannelCnt - 1; i++)
                avgRatioLi.Add(psmsTotalRatioLi[i] / psmsRatioLi.Count);

            //calculate center distance for each psm
            for (int i = 0; i < psmsRatioLi.Count; i++)
            {
                double dist = 0;
                for (int j = 0; j < paramsObj.ChannelCnt - 1; j++)//For each channel
                {
                    //avgOtherRatio: For a single channel, avg ratio of other PSMs (except the current PSM) in this protein
                    double avgOtherRatio = (psmsTotalRatioLi[j] - psmsRatioLi[i][j]) / (psmsRatioLi.Count - 1);
                    if (avgRatioLi[j] == 0)
                        dist += 0;
                    else
                    {
                        double d = Math.Abs((psmsRatioLi[i][j] - avgOtherRatio) / avgRatioLi[j]);
                        dist += Math.Pow(d, 2);
                    }
                }
                dist = Math.Round(Math.Sqrt(dist), 4);
                euDistLi.Add(dist);
            }
            return euDistLi;
        }

        /// <summary>
        /// Calculates intra-peptide and intra-protein euclidean distances of all valid PSMs in the iProphet file from ICS
        /// </summary>
        private void CalOverallEuDist()
        {
            Console.Write("Calculating euclidean distance");
            int protCnt = 0;

            //Lists for intra-protein euclidean distance 
            List<string> psmsInProtNameLi = new List<string>(); //Stores all names of PSMs in this protein
            List<List<double>> psmsInProtRatioLi = new List<List<double>>(); //Stores channel ratio of all PSMs in this protein
            List<double> psmsInProtTotalRatioLi = new List<double>(); //Stores channel ratio sum of all PSMs in this protein for further use
            List<string> singlePsmPepNameLi = new List<string>(); //If peptide only contains a single PSM, store the PSM name here
            List<List<double>> singlePsmPepRatioLi = new List<List<double>>(); //Stores channel ratio of all PSMs in singlePsmPepNameLi
            List<double> singlePsmPepTotalRatioLi = new List<double>(); //Stores channel ratio sum of all PSMs in singlePsmPepNameLi

            //Lists for intra-peptide euclidean distance
            List<string> psmsInPepNameLi = new List<string>(); //Stores all names of PSMs in this peptide
            List<List<double>> psmsInPepRatioLi = new List<List<double>>(); //Stores channel ratio of all PSMs in this peptide
            List<double> psmsInPepTotalRatioLi = new List<double>(); //Stores channel ratio sum of all PSMs in this peptide for further use

            foreach (KeyValuePair<string, ds_Protein> prot in dataContainerObj.icsIproResult.Protein_Dic)
            {
                protCnt++;
                //Write to console
                if (protCnt % 1000 == 0)
                    Console.Write(protCnt);
                else if (protCnt % 100 == 0)
                    Console.Write(".");

                for (int i = 0; i < paramsObj.ChannelCnt - 1; i++)
                {
                    psmsInProtTotalRatioLi.Add(0.0);
                    singlePsmPepTotalRatioLi.Add(0.0);
                }

                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    for (int i = 0; i < paramsObj.ChannelCnt - 1; i++)
                        psmsInPepTotalRatioLi.Add(0.0);

                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        //Check PSM validity, only calculate euclidean between valid PSMs without missing intensity
                        if (PsmIsValid(psm, pep.Value, prot.Value, this.paramsObj.IcsFdr001Prob, this.paramsObj.DecoyKeywordLi)!= 1)
                            continue;

                        //get intensity and ratio
                        List<double> psmNormIntenLi = this.normObj.GetNormIntenLi(this.paramsObj, psm.libra_ChanIntenDi.Values.ToList());
                        List<double> psmNormRatioLi = CalRatio(psmNormIntenLi);

                        //Add name and ratio to peptide and protein lists
                        psmsInPepNameLi.Add(psm.QueryNumber);
                        psmsInProtNameLi.Add(psm.QueryNumber);
                        psmsInPepRatioLi.Add(psmNormRatioLi);
                        psmsInProtRatioLi.Add(psmNormRatioLi);

                        //Add ratio to total ratio
                        for (int i = 0; i < paramsObj.ChannelCnt - 1; i++)
                        {
                            psmsInPepTotalRatioLi[i] += psmNormRatioLi[i];
                            psmsInProtTotalRatioLi[i] += psmNormRatioLi[i];
                        }
                    }

                    //Calculate intra-peptide euclidean distance and add to dictionary
                    List<double> intraPepEuDistLi = new List<double>();
                    if (psmsInPepNameLi.Count == 0) //If there are no PSM in the list
                        continue;
                    else if (psmsInPepNameLi.Count == 1)  //Add the PSM data to singlePsmPep
                    {
                        singlePsmPepNameLi.Add(psmsInPepNameLi[0]);
                        singlePsmPepRatioLi.Add(psmsInPepRatioLi[0]);
                        for (int i = 0; i < paramsObj.ChannelCnt - 1; i++)
                            singlePsmPepTotalRatioLi[i] += psmsInPepTotalRatioLi[i];
                    }
                    else  //There are more than one psms in the list
                    {
                        intraPepEuDistLi = GetEuDistFromRatio(psmsInPepRatioLi, psmsInPepTotalRatioLi);
                        for (int i = 0; i < psmsInPepNameLi.Count; i++)
                            this.dataContainerObj.icsPsmFFDic[psmsInPepNameLi[i]].IntraPepEuDist = intraPepEuDistLi[i];
                    }

                    psmsInPepNameLi.Clear();
                    psmsInPepRatioLi.Clear();
                    psmsInPepTotalRatioLi.Clear();
                }

                //Calculate intra-protein euclidean distance and store to dictionary
                if (psmsInProtNameLi.Count == 0)
                    continue;
                List<double> intraProtEuDistLi = GetEuDistFromRatio(psmsInProtRatioLi, psmsInProtTotalRatioLi);
                for (int i = 0; i < psmsInProtNameLi.Count; i++)
                    this.dataContainerObj.icsPsmFFDic[psmsInProtNameLi[i]].IntraProtEuDist = intraProtEuDistLi[i];

                //Calculate intra-peptide euclidean distance for peptides with only one PSM and store to dictionary
                if (singlePsmPepNameLi.Count == 0)
                    continue;
                else if (singlePsmPepNameLi.Count == 1)
                    this.dataContainerObj.icsPsmFFDic[singlePsmPepNameLi[0]].IntraPepEuDist = 0.0;
                else
                {
                    List<double> singlePsmIntraPepEuDistLi = GetEuDistFromRatio(singlePsmPepRatioLi, singlePsmPepTotalRatioLi);
                    for (int i = 0; i < singlePsmPepNameLi.Count; i++)
                        this.dataContainerObj.icsPsmFFDic[singlePsmPepNameLi[i]].IntraPepEuDist = singlePsmIntraPepEuDistLi[i];
                }

                //Clean lists
                psmsInProtNameLi.Clear();
                singlePsmPepNameLi.Clear();
                psmsInProtRatioLi.Clear();
                singlePsmPepRatioLi.Clear();
                psmsInProtTotalRatioLi.Clear();
                singlePsmPepTotalRatioLi.Clear();
            }
            Console.Write("\n");
            return;
        }

        /// <summary>
        /// Reads the iProphet file from ICS, filter by features and write to new file
        /// </summary>
        private void FilterIcsIproFile()
        {
            Console.WriteLine("Filtering the iProphet file from ICS and writing to new iProphet file...");
            this.logFileLines.Add("Warning: PSMs taken into account by feature filter but with missing SpectraST feature values");
            //Xml reader setup
            XmlReaderSettings readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            XmlReader icsIproReader = XmlReader.Create(Path.Combine(this.paramsObj.MainDir, this.paramsObj.IcsIproFile), readerSettings);
            XmlReader msmsRunReader = icsIproReader;
            icsIproReader.Read(); //Jump to first node
            //Xml writer setup
            XmlWriterSettings writerSettings = new XmlWriterSettings {Indent = true, IndentChars = " "};
            XmlWriter modIcsIproWriter = XmlWriter.Create(Path.Combine(this.paramsObj.MainDir, this.paramsObj.ModIcsIproFile), writerSettings);
            while (true)
            {
                if (icsIproReader.Name == "xml") //read xml header
                {
                    modIcsIproWriter.WriteNode(icsIproReader, false);
                    continue;
                }
                else if (icsIproReader.Name == "msms_pipeline_analysis" && icsIproReader.NodeType == XmlNodeType.Element) //Start element of msms_pipeline_analysis
                {
                    modIcsIproWriter.WriteStartElement(icsIproReader.Name, icsIproReader.GetAttribute("xmlns"));
                    modIcsIproWriter.WriteAttributeString("date", icsIproReader.GetAttribute("date"));
                    modIcsIproWriter.WriteAttributeString("xsi", "schemaLocation", icsIproReader.GetAttribute("xmlns:xsi"), icsIproReader.GetAttribute("xsi:schemaLocation"));
                    modIcsIproWriter.WriteAttributeString("summary_xml", icsIproReader.GetAttribute("summary_xml"));
                }
                else if (icsIproReader.Name == "msms_pipeline_analysis" && icsIproReader.NodeType == XmlNodeType.EndElement) //End element of msms_pipeline_analysis
                {
                    modIcsIproWriter.WriteEndElement();
                }
                else if (icsIproReader.Name == "analysis_summary") //Other analysis summaries
                {
                    modIcsIproWriter.WriteNode(icsIproReader, false);
                    continue;
                }
                else if (icsIproReader.Name == "msms_run_summary") //Contain PSM information
                {
                    modIcsIproWriter.WriteStartElement(icsIproReader.Name);
                    modIcsIproWriter.WriteAttributes(icsIproReader,false);
                    msmsRunReader = icsIproReader.ReadSubtree();
                    this.ReadMsmsRun(msmsRunReader, modIcsIproWriter);
                    modIcsIproWriter.WriteEndElement();
                    icsIproReader.Skip();
                    continue;
                }
                else
                    Console.WriteLine(String.Format("Warning: unexpected node in {0}: {1}", Path.Combine(this.paramsObj.MainDir, this.paramsObj.IcsIproFile), icsIproReader.Name));
                icsIproReader.Read();
                if (icsIproReader.EOF)
                    break;
            }
            //Write number of PSMs that are considered by FPF but without SpectraST features into log file
            this.logFileLines.Add(String.Format("{0} at total.", this.noSpstFeatCnt));
            
            icsIproReader.Close();
            msmsRunReader.Close();
            modIcsIproWriter.Close();
        }

        private void ReadMsmsRun(XmlReader msmsRunReader, XmlWriter modIcsIproWriter)
        {
            //Reader setup
            XmlReaderSettings readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            msmsRunReader.Read(); //Jump to first node

            while(true)
            {
                if (msmsRunReader.Name == "msms_run_summary") //itself
                    msmsRunReader.Read();
                else if (msmsRunReader.Name == "spectrum_query") //filter PSMs
                {
                    string psmName = msmsRunReader.GetAttribute("spectrum");
                    if (IsToBeFiltered(psmName) == false)
                        modIcsIproWriter.WriteNode(msmsRunReader, false);
                    else
                        msmsRunReader.Skip();
                    continue;
                }
                else if (msmsRunReader.EOF == true)
                    break;
                else //Other elements in msms_run_summary
                    modIcsIproWriter.WriteNode(msmsRunReader, false);
            }
        }

        /// <summary>
        /// Checks whether the current PSM meets any of the filtering conditions stored in the filter list object.
        /// Then returns a boolean value to indicate whether it should be removed or not.
        /// True: remove; False: keep
        /// </summary>
        /// <param name="psmName">Name for the current PSM</param>
        /// <returns></returns>
        private bool IsToBeFiltered(string psmName)
        {
            //If the PSM need not to be considered (FDR too small / shared peptide / decoy / etc.)
            if (!this.dataContainerObj.icsPsmFFDic.ContainsKey(psmName))
                return false;

            //Check whether the PSM is common (also in database search) or one of those added by spectraST
            if (this.dataContainerObj.idsPsmIdLi.Contains(psmName))
                return false;

            this.consideredCnt++;
           
            //Filtering for every feature
            this.dataContainerObj.icsPsmFFDic.TryGetValue(psmName, out ds_Psm_ForFilter psmInfoObj);
            double featValue;
            bool b_noSpstFeat = false;
            int featMeetCritCnt = 0;
            Dictionary<string, string> featMeetingCriteriaDic = new Dictionary<string, string>();
            foreach (KeyValuePair<string, List<(double lowerLim, double upperLim)>> filtsForOneFeat in filtersObj.FiltDic)
            {
                switch (filtsForOneFeat.Key)
                {
                    case "charge":
                        featValue = (double) psmInfoObj.Charge;
                        break;
                    case "mass":
                        featValue = psmInfoObj.Mass;
                        break;
                    case "peptide length":
                        featValue = (double) psmInfoObj.Peplen;
                        break;
                    case "average reporter ion intensity":
                        featValue = psmInfoObj.AvgInten;
                        break;
                    case "intra-peptide euclidean distance":
                        featValue = psmInfoObj.IntraPepEuDist;
                        break;
                    case "intra-protein euclidean distance":
                        featValue = psmInfoObj.IntraProtEuDist;
                        break;
                    case "number of ptms":
                        featValue = psmInfoObj.PtmCount;
                        break;
                    case "ptm ratio":
                        featValue = psmInfoObj.PtmRatio;
                        break;
                    case "absolute mass difference":
                        featValue = psmInfoObj.AbsMassDiff;
                        break;
                    case "absolute precursor m/z difference":
                        featValue = psmInfoObj.AbsPrecursorMzDiff;
                        break;
                    case "dot product":
                        featValue = psmInfoObj.DotProduct;
                        break;
                    case "deltad":
                        featValue = psmInfoObj.DeltaScore;
                        break;
                    case "number of hits":
                        featValue = psmInfoObj.HitsNum;
                        break;
                    case "mean of dot products of the hits":
                        featValue = psmInfoObj.HitsMean;
                        break;
                    case "standard deviation of dot products of the hits":
                        featValue = psmInfoObj.HitsStdev;
                        break;
                    case "f-value":
                        featValue = psmInfoObj.Fval;
                        break;
                    default:
                        throw new ApplicationException(String.Format("Feature name error:{0}",filtsForOneFeat.Key));
                }

                if (featValue == -10000) //In some PSMS, there may be some SpectraST features not written in iProphet file
                {
                    b_noSpstFeat = true;
                    continue;
                }
                else if (featValue == -1000) //Dealing with PSMs with missing reporter ion intensity (will not consider avg intensity / euclidean distances)
                    continue;
                else
                {
                    //If the feature should be considered
                    foreach ((double lowerLim, double upperLim) filtRange in filtsForOneFeat.Value)
                    {
                        if ((featValue >= filtRange.lowerLim) && (featValue < filtRange.upperLim))
                        {
                            featMeetingCriteriaDic.Add(filtsForOneFeat.Key, featValue.ToString());
                            featMeetCritCnt++;
                            break;
                        }
                    }
                }
            }

            //If there are missing SpectraST features, write to log file
            if (b_noSpstFeat == true)
            {
                this.logFileLines.Add(psmName + "\n");
                this.noSpstFeatCnt++;
            }

            if(featMeetCritCnt > 0)
                this.filteredOutPsmsObj.AddFilteredOutPsm(psmName, featMeetingCriteriaDic);

            if (featMeetCritCnt >= this.filtersObj.featMeetingCritNumCutoff)
            {                
                this.filteredOutCnt++;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Organize the correct format for the log file.
        /// Final format: warning_DD-MM-YYYY_HH-MM-SS
        /// </summary>
        /// <returns></returns>
        private string GetLogFileName()
        {
            string curTime = DateTime.Now.ToString(new CultureInfo("en-GB"));
            curTime = curTime.Replace('/','-');
            curTime = curTime.Replace(':', '-');
            curTime = curTime.Replace(' ', '_');
            string message = String.Format("warning_{0}.txt", curTime);
            return message;
        }
    }
}

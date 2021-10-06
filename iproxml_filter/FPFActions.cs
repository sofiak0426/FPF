using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Globalization;
using ResultReader;

namespace FPF
{
    public class FPFActions
    {
        private string mainDir;

        //Data storage
        private ds_Parameters parametersObj;
        private ds_DataContainer dataContainerObj;
        private ds_Filters filtersObj;
        private ds_Norm normObj;

        //For log file and console log
        private List<string> logFileLines; //Content of log file
        private string logFile; // Output name for log file
        int notSingleHitCnt = 0; // Number of PSMs that has more than one first-rank hit
        int noSpstFeatCnt = 0; //Number of PSMs that are considered but with missing SpectraST feature values
        int consideredCnt = 0;// Number of PSMs that are considered by FPF
        int filteredOutCnt = 0;// Number of PSM that are filtered out by FPF
        int zeroIntenCnt = 0;//test

        /// <summary>
        /// Defines thread actions by specified id. 0 for reading DB iprophet file and 1 for reading DB + SL iprophet file.
        /// </summary>
        public void DoWorkerJobs(int id)
        {
            switch (id)
            {
                case 0:
                    this.ReadIproDb();
                    break;
                case 1:
                    this.ReadIproDbSpst();
                    break;
            }
        }

        /// <summary>
        /// Performs main actions.
        /// 1. Read Parameter File
        /// 2. Read DB search iprophet file with result reader and collect PSM IDs
        /// 3. Read DB + SL iprophet file with result reader and collect PSM information
        /// 4. Calculate intra-peptide and intra-protein euclidean distance for each PSM in DB + SL iprophet file
        /// 5. Parse the DB + SL iprophet file again, filter and write to new file
        /// </summary>
        /// <param name="mainDir">Directory that stores the parameter file and all iprophet files. It is also where the new iprophet file will be written to.</param>
        /// <param name="paramFile">Parameter file name</param>
        ///
        public void MainActions(string mainDir, string paramFile)
        {
            //Initialization
            this.mainDir = mainDir;
            this.dataContainerObj = new ds_DataContainer();
            this.filtersObj = new ds_Filters();
            this.parametersObj = new ds_Parameters();
            this.normObj = new ds_Norm();
            this.logFileLines = new List<string>() {"Warning: PSM with more than one hit:"};

            //Read parameters file
            this.ReadParamFile(this.mainDir + paramFile);

            //Read the two iprophet files simultaneously with two threads
            List<int> workerIds = new List<int>{0,1};
            Parallel.ForEach(workerIds, workerId => {
                this.DoWorkerJobs(workerId);
            });

            //Collect PSM information, calculate distances and filter
            this.CollectPsmFF_FromProtein_Dic();
            this.CalEuDist();
            this.FilterDbSpstIproFile();

            //Write to log file and console
            logFile = GetLogFileName();
            File.WriteAllLines(this.mainDir + logFile, logFileLines);        
            Console.WriteLine(String.Format("FPF actions done! Examined: {0} PSMs, filtered out: {1} PSMs", consideredCnt, filteredOutCnt));
            Console.WriteLine(zeroIntenCnt);
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

                String[] lineElementsArr = line.Split(':');
                lineElementsArr[0] = lineElementsArr[0].Trim();
                lineElementsArr[1] = lineElementsArr[1].Trim();

                //Check if the parameter or feature is already specified in another line and if the name is correct or not
                string errorCode = String.Format("Error:" +
                    "have you modified the parameter to \"{0}\"?", lineElementsArr[0]);
                if (parametersObj.ValidateParamName(lineElementsArr[0])) //If the line specifies a parameter
                {
                    if(parametersObj.GetParamIsSet(lineElementsArr[0]) == true) //Check whether the param is specified by the user already
                    {
                        errorCode = String.Format("Error: you have repeatedly specify the parameter \"{0}\"", lineElementsArr[0]);
                        throw new ApplicationException(errorCode);
                    }
                    parametersObj.SetParamAsTrue(lineElementsArr[0]);

                }
                else if (filtersObj.ValidateFeatureName(lineElementsArr[0])) //If the line specifies a filter
                {
                    if (filtersObj.GetFeatureIsSet(lineElementsArr[0]) == true) //Check whether the param is specified by the user already
                    {
                        errorCode = String.Format("Error: you have repeatedly specify the filter \"{0}\"", lineElementsArr[0]);
                        throw new ApplicationException(errorCode);
                    }
                    filtersObj.SetFeatureAsTrue(lineElementsArr[0]);
                } 
                else //parameter name error
                    throw new ApplicationException(errorCode);

                //Set param or filter values
                switch (lineElementsArr[0])
                {
                    case "Database Iprophet Search File":
                        this.parametersObj.IproDbFile = lineElementsArr[1];
                        break;
                    case "Database + SpectraST Iprophet Search File":
                        this.parametersObj.IproDbSpstFile = lineElementsArr[1];
                        break;
                    case "Output File":
                        this.parametersObj.ModIproDbSpstFile = lineElementsArr[1];
                        break;
                    case "Number of Channels":
                        if (int.TryParse(lineElementsArr[1], out int channelCnt) == false)
                            throw new ApplicationException (String.Format ("Error: incorrect format for \"number of channels\": {0}", lineElementsArr[1]));
                        if (channelCnt <= 0)
                            throw new ApplicationException (String.Format ("Error: incorrect input for \"number of channels\": {0}", lineElementsArr[1]));
                        this.parametersObj.ChannelCnt = channelCnt;
                        break;
                    case "Reference Channel":
                        if (int.TryParse(lineElementsArr[1], out int refChannel) == false)
                            throw new ApplicationException (String.Format ("Error: incorrect format for \"reference channel\": {0}", lineElementsArr[1]));
                        if (refChannel <= 0)
                            throw new ApplicationException (String.Format ("Error: incorrect input for \"reference channel\": {0}", lineElementsArr[1]));
                        this.parametersObj.RefChannel = refChannel;
                        break;
                    case "Decoy Prefix":
                        this.parametersObj.DecoyPrefixArr = lineElementsArr[1].Split(',').Select(decoyPrefix => decoyPrefix.Trim()).ToArray();
                        break;
                    case "Background Keywords for Normalization":
                        if (lineElementsArr[1].ToLower() != "none")
                        {
                            string[] bgKeyArr = lineElementsArr[1].Split(',').Select(decoyPrefix => decoyPrefix.Trim()).ToArray();
                            this.normObj.AddBgProtKey(bgKeyArr); //Add background keywords to the object
                        }
                        break;
                    default: //Add feature
                        this.AddFilters(lineElementsArr[0], lineElementsArr[1]);
                        break;
                }
            }
            //Check if all the parameters are specified by the user (no need to check filter)
            List<string> missingParams = parametersObj.CheckAllParamsSet();
            if (missingParams.Count > 0) //Some of the parameters are missing
            {
                string errorcode = "Error: you didn't specify the values of the following parameters:\n";
                foreach (string missingParam in missingParams)
                    errorcode += String.Format("\"{0}\"\n", missingParam);
                throw new ApplicationException(errorcode);
            }
            //Check if reference channel is wrongly specified after all params are set
            if (this.parametersObj.RefChannel > this.parametersObj.ChannelCnt)
                throw new ApplicationException("Error: reference channel number out of range!");

            paramFileReader.Close();
            return;
        }

        /// <summary>
        /// Adds user-specified filters for each feature to filterObj for further use.
        /// </summary>
        /// <param name="feature"> Feature name</param>
        /// <param name="filterStr"> The string containing filters for a single filter, read from the param file</param>
        private void AddFilters(string feature, string filterStr)
        {
            //If the user did not specify filters of this feature
            if (filterStr.ToLower() == "none")
                return;

            String[] filterArr = filterStr.Split(',').Select(filter => filter.Trim()).ToArray();
            foreach (string filter in filterArr)
            {
                if (filter.IndexOf('-') == -1)
                    throw new ApplicationException(String.Format("Feature \"{0}\": wrong filter format \"{1}\"", feature, filterStr));

                String[] filtLimArr = filter.Split('-').Select(filtLim => filtLim.Trim()).ToArray(); //Containing strings for lower and upper limits of one filter
                (double lowerLim, double upperLim) filtLim;
                //Set up filter lower limit
                if (filtLimArr[0] == String.Empty)
                    filtLim.lowerLim = Double.NegativeInfinity;
                else
                {
                    if (double.TryParse(filtLimArr[0], out filtLim.lowerLim) == false)
                        throw new ApplicationException(String.Format("Feature {0}: wrong lower limit format", feature));
                }
                //Set up filter upper limit
                if (filtLimArr[1] == String.Empty)
                    filtLim.upperLim = Double.PositiveInfinity;
                else
                {
                    if (double.TryParse(filtLimArr[1], out filtLim.upperLim) == false)
                        throw new ApplicationException(String.Format("Feature {0}: wrong upper limit format", feature));
                }
                this.filtersObj.AddFilter(feature, filtLim);
            }
            return;
        }

        /// <summary>
        /// Only valid PSMs should be considered when filtering.
        /// Check whether the PSM is valid (not decoy prot, without shared peptide, with probability passing FDR) and whether the PSM contains zero reporter ion intensity.
        /// </summary>
        /// <param name="fdr001Prob">FDR 1% probability for the iprophet file</param>
        /// <param name="decoyPrefixArr">Array that contains all the possible decoy prefixes</param>
        /// <returns>-1 for invalid PSMs, 0 for valid PSMs but with missing reporter ion intensity, and 1 for valid PSMs without missing reporter ion intensity.</returns>
        public static int PsmIsValid(ds_PSM psm, ds_Peptide pep, ds_Protein prot, float fdr001Prob, string[] decoyPrefixArr)
        {      
            //Check if the protein is decoy
            foreach (string decoyPrefix in decoyPrefixArr)
            {
                if (prot.ProtID.StartsWith(decoyPrefix))
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
            else //search hits with no iprophet score
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
        /// 1. Parse PSMs in the DB iprophet file with result reader.
        /// 2. Add PSMs which are valid to the list.
        /// 3. Calculate normalization ratio using DB iprophet file.
        /// </summary>
        private void ReadIproDb()
        {
            Console.WriteLine("Parsing database search iprophet file...");
            PepXmlProtXmlReader iproDbReader = new PepXmlProtXmlReader();
            this.dataContainerObj.iproDbResult = iproDbReader.ReadFiles(this.mainDir + this.parametersObj.IproDbFile, "",
                XmlParser_Action.Read_PepXml, SearchResult_Source.TPP_PepXml);

            //get FDR 1% probability
            this.parametersObj.DbFdr001Prob = dataContainerObj.iproDbResult.GetPepMinProbForFDR(0.01f, "");

            //Check PSM validity
            foreach (KeyValuePair<string, ds_Protein> prot in this.dataContainerObj.iproDbResult.Protein_Dic)
            {
                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        //If PSM is valid, add PSM name to the list
                        if (PsmIsValid(psm, pep.Value, prot.Value, this.parametersObj.DbFdr001Prob, this.parametersObj.DecoyPrefixArr) != -1 
                            && !this.dataContainerObj.dbPsmIdLi.Contains(psm.QueryNumber))
                            this.dataContainerObj.dbPsmIdLi.Add(psm.QueryNumber);
                    }
                }
            }

            //Calculate ratio for normalization
            this.normObj.GetChannelMed(this.parametersObj, this.dataContainerObj.iproDbResult);
            return;
        }

        /// <summary>
        /// Parse DB + SL iprophet file with result reader.
        /// </summary>
        private void ReadIproDbSpst()
        {
            Console.WriteLine("Parsing database + spectraST search iprophet file...");
            PepXmlProtXmlReader iproDbSpstReader = new PepXmlProtXmlReader();
            this.dataContainerObj.iproDbSpstResult = iproDbSpstReader.ReadFiles(this.mainDir + this.parametersObj.IproDbSpstFile, "",
                XmlParser_Action.Read_PepXml, SearchResult_Source.TPP_PepXml);

            //get fdr < 1% probability
            this.parametersObj.DbSpstFdr001Prob = this.dataContainerObj.iproDbSpstResult.GetPepMinProbForFDR(0.01f, "");
        }

        /// <summary>
        /// Collects feature values of valid PSMs in DB + SL iprophet file to dbSpstPsmFFDic.
        /// </summary>
        private void CollectPsmFF_FromProtein_Dic()
        {
            Console.Write("Collecting PSM information");
            int psmCnt = 0; //For console
            foreach (KeyValuePair<string, ds_Protein> prot in this.dataContainerObj.iproDbSpstResult.Protein_Dic)
            {
                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        int int_isValid = PsmIsValid(psm, pep.Value, prot.Value, this.parametersObj.DbSpstFdr001Prob, this.parametersObj.DecoyPrefixArr);
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
                            List<double> psmNormIntenLi = this.normObj.GetNormIntenLi(this.parametersObj, psm.libra_ChanIntenDi.Values.ToList());
                            psmInfoObj.AvgInten = psmNormIntenLi.Average();
                        }

                        //Add information to dbSpstPsmFFDic if the current hit is the only hit for this PSM.
                        //If there is more than one hit, merely add the PSM name to log file and use the first hit.
                        try
                        {
                            this.dataContainerObj.dbSpstPsmFFDic.Add(psm.QueryNumber, psmInfoObj);
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
            //Write the total count of PSMs with more than one hit into log file
            this.logFileLines.Add(String.Format("{0} at total.", this.notSingleHitCnt));
        }

        /// <summary>
        /// Calculates ratio of each channel for a PSM and returns a list of ratios
        /// </summary>
        private List<double> CalRatio(List<double> intenLi)
        {
            List<double> ratioLi = new List<double>();
            for (int i = 0; i < this.parametersObj.ChannelCnt; i++)
            {
                if (i + 1 == this.parametersObj.RefChannel) //skip reference channel
                    continue;
                double ratio = intenLi[i] / intenLi[this.parametersObj.RefChannel - 1];
                ratioLi.Add(Math.Round(ratio, 4));
            }
            return ratioLi;
        }

        /// <summary>
        /// Given channel ratios from a set of PSMs, then return a list containing the euclidean distance of each PSM
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
            for (int i = 0; i < parametersObj.ChannelCnt - 1; i++)
                avgRatioLi.Add(psmsTotalRatioLi[i] / psmsRatioLi.Count);

            //calculate center distance for each psm
            for (int i = 0; i < psmsRatioLi.Count; i++)
            {
                double dist = 0;
                for (int j = 0; j < parametersObj.ChannelCnt - 1; j++)//For each channel
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
        /// Calculates euclidean distance of all valid PSMs in the DB + SL iprophet file
        /// </summary>
        private void CalEuDist()
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

            foreach (KeyValuePair<string, ds_Protein> prot in dataContainerObj.iproDbSpstResult.Protein_Dic)
            {
                protCnt++;
                //Write to console
                if (protCnt % 1000 == 0)
                    Console.Write(protCnt);
                else if (protCnt % 100 == 0)
                    Console.Write(".");

                for (int i = 0; i < parametersObj.ChannelCnt - 1; i++)
                {
                    psmsInProtTotalRatioLi.Add(0.0);
                    singlePsmPepTotalRatioLi.Add(0.0);
                }

                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    for (int i = 0; i < parametersObj.ChannelCnt - 1; i++)
                        psmsInPepTotalRatioLi.Add(0.0);

                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        //Check PSM validity, only calculate euclidean between valid PSMs without missing intensity
                        if (PsmIsValid(psm, pep.Value, prot.Value, this.parametersObj.DbSpstFdr001Prob, this.parametersObj.DecoyPrefixArr)!= 1)
                            continue;

                        //get intensity and ratio
                        List<double> psmNormIntenLi = this.normObj.GetNormIntenLi(this.parametersObj, psm.libra_ChanIntenDi.Values.ToList());
                        List<double> psmNormRatioLi = CalRatio(psmNormIntenLi);

                        //Add name and ratio to peptide and protein lists
                        psmsInPepNameLi.Add(psm.QueryNumber);
                        psmsInProtNameLi.Add(psm.QueryNumber);
                        psmsInPepRatioLi.Add(psmNormRatioLi);
                        psmsInProtRatioLi.Add(psmNormRatioLi);

                        //Add ratio to total ratio
                        for (int i = 0; i < parametersObj.ChannelCnt - 1; i++)
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
                        for (int i = 0; i < parametersObj.ChannelCnt - 1; i++)
                            singlePsmPepTotalRatioLi[i] += psmsInPepTotalRatioLi[i];
                    }
                    else  //There are more than one psms in the list
                    {
                        intraPepEuDistLi = GetEuDistFromRatio(psmsInPepRatioLi, psmsInPepTotalRatioLi);
                        for (int i = 0; i < psmsInPepNameLi.Count; i++)
                            this.dataContainerObj.dbSpstPsmFFDic[psmsInPepNameLi[i]].IntraPepEuDist = intraPepEuDistLi[i];
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
                    this.dataContainerObj.dbSpstPsmFFDic[psmsInProtNameLi[i]].IntraProtEuDist = intraProtEuDistLi[i];

                //Calculate intra-peptide euclidean distance for peptides with only one PSM and store to dictionary
                if (singlePsmPepNameLi.Count == 0)
                    continue;
                else if (singlePsmPepNameLi.Count == 1)
                    this.dataContainerObj.dbSpstPsmFFDic[singlePsmPepNameLi[0]].IntraPepEuDist = 0.0;
                else
                {
                    List<double> singlePsmIntraPepEuDistLi = GetEuDistFromRatio(singlePsmPepRatioLi, singlePsmPepTotalRatioLi);
                    for (int i = 0; i < singlePsmPepNameLi.Count; i++)
                        this.dataContainerObj.dbSpstPsmFFDic[singlePsmPepNameLi[i]].IntraPepEuDist = singlePsmIntraPepEuDistLi[i];
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
        /// Reads DB + SL iprophet file, filter by features and write to new file
        /// </summary>
        private void FilterDbSpstIproFile()
        {
            Console.WriteLine("Filtering database + spectraST iprophet file and writing to new iprophet...");
            this.logFileLines.Add("Warning: PSMs taken into account by feature filter but with missing Spectrast feature values");
            //Xml reader setup
            XmlReaderSettings readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            XmlReader iproDbSpstReader = XmlReader.Create(String.Format("{0}{1}",this.mainDir, this.parametersObj.IproDbSpstFile), readerSettings);
            XmlReader msmsRunReader = iproDbSpstReader;
            iproDbSpstReader.Read(); //Jump to first node
            //Xml writer setup
            XmlWriterSettings writerSettings = new XmlWriterSettings {Indent = true, IndentChars = " "};
            XmlWriter modIproDbSpstWriter = XmlWriter.Create(String.Format("{0}{1}",this.mainDir, this.parametersObj.ModIproDbSpstFile), writerSettings);
            while (true)
            {
                if (iproDbSpstReader.Name == "xml") //read xml header
                {
                    modIproDbSpstWriter.WriteNode(iproDbSpstReader, false);
                    continue;
                }
                else if (iproDbSpstReader.Name == "msms_pipeline_analysis" && iproDbSpstReader.NodeType == XmlNodeType.Element) //Start element of msms_pipeline_analysis
                {
                    modIproDbSpstWriter.WriteStartElement(iproDbSpstReader.Name, iproDbSpstReader.GetAttribute("xmlns"));
                    modIproDbSpstWriter.WriteAttributeString("date", iproDbSpstReader.GetAttribute("date"));
                    modIproDbSpstWriter.WriteAttributeString("xsi", "schemaLocation", iproDbSpstReader.GetAttribute("xmlns:xsi"), iproDbSpstReader.GetAttribute("xsi:schemaLocation"));
                    modIproDbSpstWriter.WriteAttributeString("summary_xml", iproDbSpstReader.GetAttribute("summary_xml"));
                }
                else if (iproDbSpstReader.Name == "msms_pipeline_analysis" && iproDbSpstReader.NodeType == XmlNodeType.EndElement) //End element of msms_pipeline_analysis
                {
                    modIproDbSpstWriter.WriteEndElement();
                }
                else if (iproDbSpstReader.Name == "analysis_summary") //Other analysis summaries
                {
                    modIproDbSpstWriter.WriteNode(iproDbSpstReader, false);
                    continue;
                }
                else if (iproDbSpstReader.Name == "msms_run_summary") //Contain PSM information
                {
                    modIproDbSpstWriter.WriteStartElement(iproDbSpstReader.Name);
                    modIproDbSpstWriter.WriteAttributes(iproDbSpstReader,false);
                    msmsRunReader = iproDbSpstReader.ReadSubtree();
                    ReadMsmsRun(msmsRunReader, modIproDbSpstWriter);
                    modIproDbSpstWriter.WriteEndElement();
                    iproDbSpstReader.Skip();
                    continue;
                }
                else
                    Console.WriteLine(String.Format("Warning: unexpected node in {0}: {1}", this.mainDir + this.parametersObj.IproDbSpstFile, iproDbSpstReader.Name));
                iproDbSpstReader.Read();
                if (iproDbSpstReader.EOF == true)
                    break;
            }
            //Write number of PSMs that are considered by FPF but without SpectraST features into log file
            this.logFileLines.Add(String.Format("{0} at total.", this.noSpstFeatCnt));
            
            iproDbSpstReader.Close();
            msmsRunReader.Close();
            modIproDbSpstWriter.Close();
        }

        private void ReadMsmsRun(XmlReader msmsRunReader, XmlWriter modIproDbSpstWriter)
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
                    if (FilterPsm(psmName) == false)
                        modIproDbSpstWriter.WriteNode(msmsRunReader, false);
                    else
                        msmsRunReader.Skip();
                    continue;
                }
                else if (msmsRunReader.EOF == true)
                    break;
                else //Other elements in msms_run_summary
                    modIproDbSpstWriter.WriteNode(msmsRunReader, false);
            }
        }

        /// <summary>
        /// Checks whether the current PSM meets any of the filtering conditions stored in the filter list object.
        /// Then returns a boolean value to indicate whether it should be removed or not.
        /// True: remove; False: keep
        /// </summary>
        /// <param name="psmName">Name for the current PSM</param>
        /// <returns></returns>
        private bool FilterPsm(string psmName)
        {
            //If the PSM need not to be considered (FDR too small / shared peptide / decoy / etc.)
            if (!this.dataContainerObj.dbSpstPsmFFDic.ContainsKey(psmName))
                return false;

            //Check whether the PSM is common (also in database search) or one of those added by spectraST
            if (this.dataContainerObj.dbPsmIdLi.Contains(psmName))
                return false;

            this.consideredCnt++;
           
            //Filtering for every feature
            this.dataContainerObj.dbSpstPsmFFDic.TryGetValue(psmName, out ds_Psm_ForFilter psmInfoObj);
            double featValue;
            bool b_noSpstFeat = false;
            bool isFilteredOut = false;
            foreach (KeyValuePair<string, List<(double lowerLim, double upperLim)>> filtsForOneFeat in filtersObj.FiltDic)
            {
                switch (filtsForOneFeat.Key)
                {
                    case "Charge":
                        featValue = (double) psmInfoObj.Charge;
                        break;
                    case "Mass":
                        featValue = psmInfoObj.Mass;
                        break;
                    case "Peptide Length":
                        featValue = (double) psmInfoObj.Peplen;
                        break;
                    case "Average Reporter Ion Intensity":
                        featValue = psmInfoObj.AvgInten;
                        break;
                    case "Intra-Peptide Euclidean Distance":
                        featValue = psmInfoObj.IntraPepEuDist;
                        break;
                    case "Intra-Protein Euclidean Distance":
                        featValue = psmInfoObj.IntraProtEuDist;
                        break;
                    case "Number of PTMs":
                        featValue = psmInfoObj.PtmCount;
                        break;
                    case "PTM Ratio":
                        featValue = psmInfoObj.PtmRatio;
                        break;
                    case "Absolute Mass Difference":
                        featValue = psmInfoObj.AbsMassDiff;
                        break;
                    case "Absolute Precursor Mz Difference":
                        featValue = psmInfoObj.AbsPrecursorMzDiff;
                        break;
                    case "Dot Product":
                        featValue = psmInfoObj.DotProduct;
                        break;
                    case "DeltaD":
                        featValue = psmInfoObj.DeltaScore;
                        break;
                    case "Number of Hits":
                        featValue = psmInfoObj.HitsNum;
                        break;
                    case "Mean of Dot Products of the Hits":
                        featValue = psmInfoObj.HitsMean;
                        break;
                    case "Standard Deviation of Dot Products of the Hits":
                        featValue = psmInfoObj.HitsStdev;
                        break;
                    case "F-value":
                        featValue = psmInfoObj.Fval;
                        break;
                    default:
                        throw new ApplicationException(String.Format("Feature name error:{0}",filtsForOneFeat.Key));
                }

                if (featValue == -10000) //In some PSMS, there may be some SpectraST features not written in iprophet file
                {
                    b_noSpstFeat = true;
                    continue;
                }
                else if (featValue == -1000) //Dealing with PSMs with missing reporter ion intensity (will not consider avg intenstiy / euclidean distances)
                    continue;
                else
                {
                    //If the feature should be considered
                    foreach ((double lowerLim, double upperLim) filtRange in filtsForOneFeat.Value)
                    {
                        if ((featValue >= filtRange.lowerLim) && (featValue < filtRange.upperLim))
                        { 
                            isFilteredOut = true;
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

            if (psmInfoObj.IntraPepEuDist == -1000 && isFilteredOut == true)
                this.zeroIntenCnt++;

            if (isFilteredOut == true)
                this.filteredOutCnt++;
            return isFilteredOut;
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

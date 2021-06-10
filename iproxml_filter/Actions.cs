using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Globalization;
using ResultReader;

namespace iproxml_filter
{

    public class FPFActions
    {
        private string mainDir;

        //Data storage
        private ds_Parameters parametersObj;
        private ds_DataContainer dataContainerObj;
        private ds_Filters filtersObj;
        private List<string> logFileLines;
        private string logFile;
        //List<string> result = new List<string> (); //for testing
        //int added = 0;//for testing
        int remove = 0;//for testing, how many PSMs are filtered out

        /// <summary>
        /// Defines thread actions by specified id
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
        /// 2. Read PSM names from database search iprophet file
        /// 3. Read PSM information with search result reader from database and spectrast search iprophet file
        /// 4. Calculate intra-peptide and intra-protein euclidean distance
        /// 5. Parse the database and spectrast search iprophet file again, filter and write to new file
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
            this.logFileLines = new List<string>();

            //Read parameters file
            this.ReadParamFile(this.mainDir + paramFile);

            List<int> workerIds = new List<int>{0,1};
            Parallel.ForEach(workerIds, workerId => {
                this.DoWorkerJobs(workerId);
            });

            this.CollectPsmFF_FromProtein_Dic();
            this.CalEuDist();
            this.FilterDbSpstIproFile();
            /*
            using StreamWriter f = new StreamWriter(this.mainDir + "test.txt"); 
            foreach (string r in result)
                f.WriteLine(r);
            */
            logFile = GetLogFileName();
            File.WriteAllLines(this.mainDir + logFile, logFileLines);
            Console.WriteLine("Done!");
            return;       
        }

        /// <summary>
        /// Reads the parameter file line by line and checks whether the parameter name corresponds to the correct 
        /// parameter names. If correct, then modifies global variables.
        /// </summary>
        /// <param name="paramFile">Paramter file name</param>
        private void ReadParamFile(string paramFile)
        {
            Console.WriteLine("Reading parameter file...");
            string line;
            using StreamReader paramFileReader = new StreamReader(paramFile);
            while ((line = paramFileReader.ReadLine()) != null)
            {
                //Skip anotations or empty lines
                if (line == "")
                    continue;
                else if (line[0] == '#')
                    continue;

                String[] lineElementsArr = line.Split(':');
                lineElementsArr[0] = lineElementsArr[0].Trim();
                lineElementsArr[1] = lineElementsArr[1].Trim();

                string errorCode = String.Format("Parameter error:" +
                    "Have you modified the parameter to \"{0}\"?", lineElementsArr[0]);
                if (parametersObj.ValidateParamDescription(lineElementsArr[0])) //If the line specifies a parameter
                {
                    if(parametersObj.GetParamIsSet(lineElementsArr[0]) == true)//Check whether the param is specified by the user already
                    {
                        errorCode = String.Format("You have repeatedly specify the parameter \"{0}\"", lineElementsArr[0]);
                        throw new ApplicationException(errorCode);
                    }
                }
                else if (filtersObj.featureNameLi.Contains(lineElementsArr[0])) { } //If the line specifies a filter, then continue
                else //parameter name error
                    throw new ApplicationException(errorCode);

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
                    case "Channel Number": //Channel Number
                        int.TryParse(lineElementsArr[1], out int channelCnt);
                        this.parametersObj.ChannelCnt = channelCnt;
                        break;
                    case "Reference Channel":
                        int.TryParse(lineElementsArr[1], out int refChannel);
                        this.parametersObj.RefChannel = refChannel;
                        break;
                    case "Decoy Prefix":
                        this.parametersObj.DecoyPrefix = lineElementsArr[1];
                        break;
                    default: //Add feature
                        this.AddFilters(lineElementsArr[0], lineElementsArr[1]);
                        break;
                }
                parametersObj.SetParamAsTrue(lineElementsArr[0]);
            }
            //Check if all the parameters are specified by the user
            List<string> missingParams = parametersObj.CheckAllParamsSet();
            string errorcode = "You didn't specify the values of the following parameters:\n";
            if (missingParams.Count > 0) //Some of the parameters are missing
            {
                foreach (string missingParam in missingParams)
                    errorcode += String.Format("\"{0}\"\n", missingParam);
                throw new ApplicationException(errorcode);
            }
            paramFileReader.Close();
            return;
        }

        /// <summary>
        /// Adds filter ranges for each feature, as specified by the user in the param file, to filter list for further use.
        /// </summary>
        /// <param name="feature"> Specify the feature name</param>
        /// <param name="filterStr">The string containing ranges of filters, read from the param file</param>
        private void AddFilters(string feature, string filterStr)
        {
            //If the user did not specify filters of this feature
            if (filterStr.ToLower() == "none")
                return;

            String[] filterArr = filterStr.Split(',');
            foreach (string filter in filterArr)
            {
                if (filter.Trim().IndexOf('-') == -1)
                    throw new ApplicationException(String.Format("Feature \"{0}\": Wrong filter format \"{1}\"", feature, filterStr));

                String[] filtLimArr = filter.Trim().Split('-');
                (double lowerLim, double upperLim) filtLim;
                //Set up filter lower limit
                if (filtLimArr[0] == String.Empty)
                    filtLim.lowerLim = Double.NegativeInfinity;
                else
                {
                    try{
                        double.TryParse(filtLimArr[0], out filtLim.lowerLim);
                    }
                    catch{
                        throw new ApplicationException(String.Format("Feature {0}: Wrong lower limit format", feature));
                    }
                }
                //Set up filter upper limit
                if (filtLimArr[1] == String.Empty)
                    filtLim.upperLim = Double.PositiveInfinity;
                else
                {
                    try{
                        double.TryParse(filtLimArr[1], out filtLim.upperLim);
                    }
                    catch{
                        throw new ApplicationException(String.Format("Feature {0}: Wrong upper limit format", feature));
                    }
                }
                this.filtersObj.AddFilter(feature, filtLim);
            }
            return;
        }

        /// <summary>
        /// Only valid PSMs should be considered when filtering.
        /// Check whther the PSM is valid (not decoy prot, without shared peptide, with probability passing FDR,
        /// and no channel missing values)
        /// PSM with missing reporter ion is always invalid.
        /// </summary>
        private bool PsmIsValid(ds_PSM psm, ds_Peptide pep, ds_Protein prot, float fdr001Prob)
        {
            //Ignore decoy proteins
            if (prot.ProtID.StartsWith(this.parametersObj.DecoyPrefix))
                return false;
            //Ignore psms with shared peptide
            if (pep.b_IsUnique == false)
                return false;
            //Ignore psms with interprophet probability < 1% FDR probability
            string keyScoreType = "peptideprophet_result";
            Dictionary<string, double> psmScoreDic = (Dictionary<string, double>)psm.Score;
            double psmScore = 0;
            if (psmScoreDic.ContainsKey(keyScoreType))
                psmScore = psmScoreDic[keyScoreType];
            else //search hits with no iprophet score
                return false;
            if (psmScore < fdr001Prob)
                return false;
            //Ignore psms with any missing intensity value
            List<double> psmIntenLi = new List<double>();
            psmIntenLi.AddRange(psm.libra_ChanIntenDi.Values);
            if (psmIntenLi.Contains(0))
                return false;
            return true;
        }
        
        /// <summary>
        /// Parse PSMs in the database search iprophet file and select those which are valid
        /// (probability > 1% fdr, not decoy, not shared peptide, without missing intensity values).
        /// </summary>
        private void ReadIproDb()
        {
            Console.WriteLine("Parsing database search iprophet file...");
            PepXmlProtXmlReader iproDbReader = new PepXmlProtXmlReader();
            this.dataContainerObj.iproDbResult = iproDbReader.ReadFiles(this.mainDir + this.parametersObj.IproDbFile, "",
                XmlParser_Action.Read_PepXml, SearchResult_Source.TPP_PepXml);

            //get fdr < 1% probability
            this.parametersObj.DbFdr001Prob = dataContainerObj.iproDbResult.GetPepMinProbForFDR(0.01f, "");

            //Check PSM validity
            foreach (KeyValuePair<string, ds_Protein> prot in this.dataContainerObj.iproDbResult.Protein_Dic)
            {
                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        //If PSM is valid, add PSM name to the list
                        if (PsmIsValid(psm, pep.Value, prot.Value, this.parametersObj.DbFdr001Prob))
                            this.dataContainerObj.dbPsmIdLi.Add(psm.QueryNumber);
                    }
                }
            }
            return;
        }

        /// <summary>
        /// Parse database + spectraST serach iprophet file with search result reader
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
        ///For each PSM in the database + spectraST search iprophet file, 
        ///collects feature values that are required for filtering to psmInfoDic.
        /// </summary>
        private void CollectPsmFF_FromProtein_Dic()
        {
            Console.WriteLine("Collecting PSM information...");
            foreach (KeyValuePair<string, ds_Protein> prot in this.dataContainerObj.iproDbSpstResult.Protein_Dic)
            {
                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        if (!PsmIsValid(psm, pep.Value, prot.Value, this.parametersObj.DbSpstFdr001Prob))
                            continue;
                        Dictionary<string, double> psmScoreDic = (Dictionary<string, double>)psm.Score;
                        Dictionary<string, double> spstScoreDic = new Dictionary<string, double>(); //Store scores specific for SpectraST
                        List<string> scoreNames = new List<string> { "dot", "delta", "precursor_mz_diff", "hits_num",
                            "hits_mean", "hits_stdev", "fval" };
                        foreach (string scoreName in scoreNames)
                        {
                            if (psmScoreDic.ContainsKey(scoreName)) //For Spectrast-added PSMs
                                spstScoreDic.Add(scoreName, psmScoreDic[scoreName]);
                            else //For PSMs without spectraST features
                                spstScoreDic.Add(scoreName, (double)-10000);
                        }
                        ds_Psm_ForFilter psmInfoObj = new ds_Psm_ForFilter(
                            psm.Pep_exp_mass, //Mass
                            psm.Charge, //Charge
                            pep.Value.Sequence.Length, //Peptide length
                            pep.Value.ModPosList.Count, //PTM count
                            (double)pep.Value.ModPosList.Count / pep.Value.Sequence.Length, //PTM ratio
                            Math.Abs(psm.MassError), //Absolute Mass Difference
                            Math.Abs(spstScoreDic["precursor_mz_diff"]), //Absolute Precursor Mz Difference
                            spstScoreDic["dot"], //Dot Product
                            spstScoreDic["delta"], //Delta Score
                            spstScoreDic["hits_num"], //Hits Num
                            spstScoreDic["hits_mean"], //Hits Mean
                            spstScoreDic["hits_stdev"], //Hits Standard Deviation
                            spstScoreDic["fval"] //f-value
                            );
                        List<double> psmIntenLi = new List<double>();
                        psmIntenLi.AddRange(psm.libra_ChanIntenDi.Values);
                        psmInfoObj.AvgInten = psmIntenLi.Average();
                        this.dataContainerObj.dbSpstPsmFFDic.Add(psm.QueryNumber, psmInfoObj);
                    }
                }
            }
        }

        /// <summary>
        ///  calculate channel ratios of psms and returns a list 
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
        /// Given channel ratios from a set of PSMs, then return a list containing PSMs' euclidean distance with other PSMs in the set
        /// </summary>
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
            for (int i = 0; i < this.parametersObj.ChannelCnt - 1; i++)
                avgRatioLi.Add(psmsTotalRatioLi[i] / psmsRatioLi.Count);

            //calculate euclidean for each psm
            for (int i = 0; i < psmsRatioLi.Count; i++)
            {
                double dist = 0.0;
                for (int j = 0; j < this.parametersObj.ChannelCnt - 1; j++)//For each channel
                {
                    //avgOtherRatio: For a single channel, avg ratio of other PSMs (except the current PSM) in this protein
                    double avgOtherRatio = (psmsTotalRatioLi[j] - psmsRatioLi[i][j]) / (psmsRatioLi.Count - 1);
                    double d = Math.Abs((psmsRatioLi[i][j] - avgOtherRatio) / avgRatioLi[j]);
                    dist += Math.Pow(d, 2);
                }
                dist = Math.Round(Math.Sqrt(dist),4);
                euDistLi.Add(dist);
            }
            return euDistLi;
        }

        /// <summary>
        /// Calculates euclidean distance of all PSMs in the database+spectaST search iprophet file
        /// </summary>
        private void CalEuDist()
        {
            Console.WriteLine("Calculating euclidean distance...");
            foreach (KeyValuePair<string, ds_Protein> prot in dataContainerObj.iproDbSpstResult.Protein_Dic)
            {
                //Lists for intra-protein euclidean distance 
                List<string> psmsInProtNameLi = new List<string>(); //Stores all names of PSMs in this protein
                List<List<double>> psmsInProtRatioLi = new List<List<double>>(); //Stores channel ratio of all PSMs in this protein
                List<double> psmsInProtTotalRatioLi = new List<double>(); //Stores channel ratio sum of all PSMs in this protein for further use
                List<string> singlePsmPepNameLi = new List<string>(); //If peptide only contains a single PSM, store the PSM name here
                List<List<double>> singlePsmPepRatioLi = new List<List<double>>(); //Stores channel ratio of all PSMs in singlePsmPepNameLi
                List<double> singlePsmPepTotalRatioLi = new List<double>(); //Stores channel ratio sum of all PSMs in singlePsmPepNameLi
                for (int i = 0; i < this.parametersObj.ChannelCnt - 1; i++)
                {
                    psmsInProtTotalRatioLi.Add(0.0);
                    singlePsmPepTotalRatioLi.Add(0.0);
                }

                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    //Lists for intra-peptide euclidean distance
                    List<string> psmsInPepNameLi = new List<string>(); //Stores all names of PSMs in this peptide
                    List<List<double>> psmsInPepRatioLi = new List<List<double>>(); //Stores channel ratio of all PSMs in this peptide
                    List<double> psmsInPepTotalRatioLi = new List<double>(); //Stores channel ratio sum of all PSMs in this peptide for further use
                    for (int i = 0; i < this.parametersObj.ChannelCnt - 1; i++)
                        psmsInPepTotalRatioLi.Add(0.0);

                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        //Check PSM validity
                        if (!PsmIsValid(psm, pep.Value, prot.Value, this.parametersObj.DbSpstFdr001Prob))
                            continue;

                        //get intensity and ratio
                        List<double> psmIntenLi = new List<double>();
                        psmIntenLi.AddRange(psm.libra_ChanIntenDi.Values);
                        List<double> psmRatioLi = CalRatio(psmIntenLi);

                        //Add name and ratio to peptide and protein lists
                        psmsInPepNameLi.Add(psm.QueryNumber);
                        psmsInProtNameLi.Add(psm.QueryNumber);
                        psmsInPepRatioLi.Add(psmRatioLi);
                        psmsInProtRatioLi.Add(psmRatioLi);

                        //Add ratio to total ratio
                        for (int i = 0; i < this.parametersObj.ChannelCnt - 1; i++)
                        {
                            psmsInPepTotalRatioLi[i] += psmRatioLi[i];
                            psmsInProtTotalRatioLi[i] += psmRatioLi[i];
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
                        for (int i = 0; i < this.parametersObj.ChannelCnt - 1; i++)
                            singlePsmPepTotalRatioLi[i] += psmsInPepTotalRatioLi[i];
                    }
                    else  //There are more than one psms in the list
                    {
                        intraPepEuDistLi = GetEuDistFromRatio(psmsInPepRatioLi, psmsInPepTotalRatioLi);
                        for (int i = 0; i < psmsInPepNameLi.Count; i++)
                        {
                            this.dataContainerObj.dbSpstPsmFFDic[psmsInPepNameLi[i]].IntraPepEuDist = intraPepEuDistLi[i];
                        }
                    }
                }

                //Calculate intra-protein euclidean distance and store to dictionary
                List<double> intraProtEuDistLi = GetEuDistFromRatio(psmsInProtRatioLi, psmsInProtTotalRatioLi);
                if (psmsInProtNameLi.Count == 0)
                    continue;
                for (int i = 0; i < psmsInProtNameLi.Count; i++)
                {
                    this.dataContainerObj.dbSpstPsmFFDic[psmsInProtNameLi[i]].IntraProtEuDist = intraProtEuDistLi[i];
                }

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
            }
            return;
        }

        /// <summary>
        /// Read database + spectraST search iprophet file, filter and write to new file
        /// </summary>
        private void FilterDbSpstIproFile()
        {
            Console.WriteLine("Filtering database + spectraST iprophet file and writing to new iprophet...");
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

                if (msmsRunReader.Name == "spectrum_query") //filter PSMs
                {
                    string psmName = msmsRunReader.GetAttribute("spectrum");
                    if (FilterPsm(psmName) == false)
                        modIproDbSpstWriter.WriteNode(msmsRunReader, false);
                    else
                        msmsRunReader.Skip();
                    continue;               
                }

                if (msmsRunReader.EOF == true)
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
            //If the PSM need not to be considered (FDR too small / shared peptide / missing value / etc.)
            if (!this.dataContainerObj.dbSpstPsmFFDic.ContainsKey(psmName))
                return false;

            //Check whether the PSM is common (also in database search) or one of those added by spectraST
            if (this.dataContainerObj.dbPsmIdLi.Contains(psmName))
                return false;

            //this.added ++;
            //Filtering for every feature
            //Console.WriteLine(String.Format("{0}: added PSM", psmName));
            this.dataContainerObj.dbSpstPsmFFDic.TryGetValue(psmName, out ds_Psm_ForFilter psmInfoObj);
            double featValue;
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
                        featValue = (double)psmInfoObj.Peplen;
                        break;
                    case "Average Intensity":
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

                foreach ((double lowerLim, double upperLim) filtRange in filtsForOneFeat.Value)
                {
                    if (featValue == -10000) //In some PSMS, there may be some SpectraST features not written in iprophet file
                    {
                        this.logFileLines.Add(psmName + "\n");
                        break;
                    }
                    if ((featValue >= filtRange.lowerLim) && (featValue < filtRange.upperLim))
                    {
                        //this.result.Add(psmName);
                        this.remove++;
                        return true;
                    }
                }
            }      
            return false;
        }

        /// <summary>
        /// Organize the correct format for the log file.
        /// Final format: DD-MM-YYYY_HH-MM-SS_log
        /// </summary>
        /// <returns></returns>
        private string GetLogFileName()
        {
            string curTime = DateTime.Now.ToString(new CultureInfo("en-GB"));
            curTime = curTime.Replace('/','-');
            curTime = curTime.Replace(':', '-');
            curTime = curTime.Replace(' ', '_');
            curTime += "_log.txt";
            return curTime;
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using ResultReader;

namespace iproxml_filter
{

    public class Actions
    {
        //Global Variables
        private string mainDir;
        private string iproDbFile;
        private string iproDbSpstFile;
        private string modIproDbSpstFile;
        private int channelCnt;
        private int refChan;
        private string decoyPrefix;
        private float dbFdr001Prob;
        private float dbSpstFdr001Prob;

        //Data storage
        public ds_DataContainer dataContainerObj;
        public ds_Filter filterParamObj;
        List<string> result = new List<string> (); //for testing
        int added = 0;

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
            //Read parameters file
            this.mainDir = mainDir;
            this.dataContainerObj = new ds_DataContainer();
            this.filterParamObj = new ds_Filter();
            this.ReadParamFile(this.mainDir + paramFile);

            List<int> workerIds = new List<int>{0,1};
            Parallel.ForEach(workerIds, workerId => {
                this.DoWorkerJobs(workerId);
            });

            this.AddPsmInfo();
            this.CalEuDist();
            this.FilterDbSpstIproFile();
            /*
            using StreamWriter f = new StreamWriter(this.mainDir + "test.txt"); 
            foreach (string r in result)
                f.WriteLine(r);
            */
            Console.WriteLine("Done!");
            return;
           
        }

        /// <summary>
        /// Reads the parameter file line by line and checks whether the parameter name for each line corresponds to the correct 
        /// parameter names. If correct, then modifies global variables.
        /// </summary>
        /// <param name="paramFile">Paramter file name</param>
        private void ReadParamFile(string paramFile)
        {
            Console.WriteLine("Reading parameter file...");
            string line;
            using StreamReader paramFileReader = new StreamReader(paramFile);
            int lineCnt = 0; //count current number of valid lines
            while ((line = paramFileReader.ReadLine()) != null)
            {
                //Skip anotations or empty lines
                if (line[0] == '#' || line == "\n")
                    continue;
                lineCnt++;

                String[] lineElementsArr = line.Split(':');
                lineElementsArr[1] = lineElementsArr[1].Trim(' ');

                ParameterType correctParam = (ParameterType)lineCnt;
                ds_Parameters.parameterDic.TryGetValue(correctParam, out string correctParamStr); //Get the correct parameter string for this line
                string errorCode = String.Format("Parameter error:" +
                    "Have you modified the parameter {0} to \"{1}\"?",
                    correctParamStr, lineElementsArr[0]);

                //parameter names does not match default
                if (lineElementsArr[0] != correctParamStr)
                    throw new ApplicationException(errorCode);

                //if a feature filter is empty, skip the feature
                if (lineCnt >= 7 && String.IsNullOrEmpty(lineElementsArr[1]))
                    continue;
        
                switch (correctParam)
                {
                    case ParameterType.DbIproFile: //Database Iprophet Search File:
                        this.iproDbFile = lineElementsArr[1];
                        break;
                    case ParameterType.DbSpstIproFile: //"Database + SpectraST Iprophet Search File"
                        this.iproDbSpstFile = lineElementsArr[1];
                        break;
                    case ParameterType.OutputFile:
                        this.modIproDbSpstFile = lineElementsArr[1];
                        break;
                    case ParameterType.ChannelNum: //Channel Number
                        int.TryParse(lineElementsArr[1], out this.channelCnt);
                        break;
                    case ParameterType.RefChan: //Reference Channel
                        int.TryParse(lineElementsArr[1], out this.refChan);
                        break;
                    case ParameterType.DecoyPrefix:
                        this.decoyPrefix = lineElementsArr[1];
                        break;
                    default: //Add feature
                        if(this.AddFilters(correctParamStr, lineElementsArr[1]) == false)
                            throw new ApplicationException("");
                        break;
                }
            }
            paramFileReader.Close();
            return;
        }

        /// <summary>
        /// Adds filter ranges for each feature, as specified by the user in the param file, to filter list for further use.
        /// </summary>
        /// <param name="feature"> Specify the feature name</param>
        /// <param name="filterStr">The string containing ranges of filters, read from the param file</param>
        private bool AddFilters(string feature, string filterStr)
        {
            String[] filterArr = filterStr.Split(',');
            foreach (string filter in filterArr)
            {
                if (filter.Trim(' ').IndexOf('-') == -1)
                    throw new ApplicationException(String.Format("Feature {0}: Wrong filter format", feature));

                String[] filtLimArr = filter.Trim(' ').Split('-');
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
                this.filterParamObj.AddFilter(feature, filtLim);
            }
            return true;
        }

        /// <summary>
        /// Check whther the PSM is valid (not decoy prot, without shared peptide, with probability passing FDR,
        /// and no channel missing values) since only valid PSMs should be considered when filtering.
        /// </summary>
        private bool PsmIsValid(ds_PSM psm, ds_Peptide pep, ds_Protein prot, float fdr001Prob)
        {
            //Ignore decoy proteins
            if (prot.ProtID.StartsWith(decoyPrefix))
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
            //Ignore psms with no missing intensity value
            List<double> psmIntenLi = new List<double>();
            psmIntenLi.AddRange(psm.Libra_ChanIntenDi.Values);
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
            this.dataContainerObj.iproDbResult = iproDbReader.ReadFiles(this.mainDir + this.iproDbFile, "",
                XmlParser_Action.Read_PepXml, SearchResult_Source.TPP_PepXml);

            //get fdr < 1% probability
            this.dbFdr001Prob = dataContainerObj.iproDbResult.GetPepMinProbForFDR(0.01f, "");

            //Check PSM validity
            foreach (KeyValuePair<string, ds_Protein> prot in this.dataContainerObj.iproDbResult.Protein_Dic)
            {
                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        //If PSM is valid, add PSM name to the list
                        if (PsmIsValid(psm, pep.Value, prot.Value, this.dbFdr001Prob))
                            this.dataContainerObj.dbPsmNameLi.Add(psm.QueryNumber);
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
            this.dataContainerObj.iproDbSpstResult = iproDbSpstReader.ReadFiles(this.mainDir + this.iproDbSpstFile, "",
                XmlParser_Action.Read_PepXml, SearchResult_Source.TPP_PepXml);

            //get fdr < 1% probability
            this.dbSpstFdr001Prob = dataContainerObj.iproDbSpstResult.GetPepMinProbForFDR(0.01f, "");
        }

        /// <summary>
        ///For each PSM in the database + spectraST search iprophet file, 
        ///collects feature values that are required for filtering to psmInfoDic.
        /// </summary>
        private void AddPsmInfo()
        {
            Console.WriteLine("Collecting PSM information...");
            foreach (KeyValuePair<string, ds_Protein> prot in this.dataContainerObj.iproDbSpstResult.Protein_Dic)
            {
                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        if (!PsmIsValid(psm, pep.Value, prot.Value, this.dbSpstFdr001Prob))
                            continue;
                        ds_PsmInfo psmInfoObj = new ds_PsmInfo(psm.Pep_exp_mass, psm.Charge, pep.Value.Sequence.Length);
                        List<double> psmIntenLi = new List<double>();
                        psmIntenLi.AddRange(psm.Libra_ChanIntenDi.Values);
                        psmInfoObj.SetFeatureValue("Average Intensity", psmIntenLi.Average());
                        this.dataContainerObj.dbSpstPsmInfoDic.Add(psm.QueryNumber, psmInfoObj);
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
            for (int i = 0; i < channelCnt; i++)
            {
                if (i + 1 == refChan) //skip reference channel
                    continue;
                double ratio = intenLi[i] / intenLi[refChan - 1];
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
            for (int i = 0; i < this.channelCnt - 1; i++)
                avgRatioLi.Add(psmsTotalRatioLi[i] / psmsRatioLi.Count);

            //calculate euclidean for each psm
            for (int i = 0; i < psmsRatioLi.Count; i++)
            {
                double dist = 0.0;
                for (int j = 0; j < this.channelCnt - 1; j++)//For each channel
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
                for (int i = 0; i < this.channelCnt - 1; i++)
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
                    for (int i = 0; i < this.channelCnt - 1; i++)
                        psmsInPepTotalRatioLi.Add(0.0);

                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        //Check PSM validity
                        if (!PsmIsValid(psm, pep.Value, prot.Value, dbSpstFdr001Prob))
                            continue;

                        //get intensity and ratio
                        List<double> psmIntenLi = new List<double>();
                        psmIntenLi.AddRange(psm.Libra_ChanIntenDi.Values);
                        List<double> psmRatioLi = CalRatio(psmIntenLi);

                        //Add name and ratio to peptide and protein lists
                        psmsInPepNameLi.Add(psm.QueryNumber);
                        psmsInProtNameLi.Add(psm.QueryNumber);
                        psmsInPepRatioLi.Add(psmRatioLi);
                        psmsInProtRatioLi.Add(psmRatioLi);

                        //Add ratio to total ratio
                        for (int i = 0; i < this.channelCnt - 1; i++)
                        {
                            psmsInPepTotalRatioLi[i] += psmRatioLi[i];
                            psmsInProtTotalRatioLi[i] += psmRatioLi[i];
                        }
                    }

                    //Calculate intra-peptide euclidean distance and add to dictionary
                    List<double> intraPepEuDistLi = new List<double>();
                    if (psmsInPepNameLi.Count != 1) //There are more than one psms in the list
                    {
                        intraPepEuDistLi = GetEuDistFromRatio(psmsInPepRatioLi, psmsInPepTotalRatioLi);
                        for (int i = 0; i < psmsInPepNameLi.Count; i++)
                        {
                            this.dataContainerObj.dbSpstPsmInfoDic[psmsInPepNameLi[i]].SetFeatureValue("Intra-Peptide Euclidean Distance",
                                intraPepEuDistLi[i]);
                        }
                    }
                    else //Add the PSM data to singlePsmPep 
                    {
                        singlePsmPepNameLi.Add(psmsInPepNameLi[0]);
                        singlePsmPepRatioLi.Add(psmsInPepRatioLi[0]);
                        for (int i = 0; i < this.channelCnt - 1; i++)
                            singlePsmPepTotalRatioLi[i] += psmsInPepTotalRatioLi[i];
                    }
                }

                //Calculate intra-protein euclidean distance and store to dictionary
                List<double> intraProtEuDistLi = GetEuDistFromRatio(psmsInProtRatioLi, psmsInProtTotalRatioLi);
                for (int i = 0; i < psmsInProtNameLi.Count; i++)
                {
                    this.dataContainerObj.dbSpstPsmInfoDic[psmsInProtNameLi[i]].SetFeatureValue("Intra-Protein Euclidean Distance",
                        intraProtEuDistLi[i]);
                }

                //Calculate intra-peptide euclidean distance for peptides with only one PSM and store to dictionary
                if (singlePsmPepNameLi.Count == 0)
                    continue;
                else if (singlePsmPepNameLi.Count == 1)
                    this.dataContainerObj.dbSpstPsmInfoDic[singlePsmPepNameLi[0]].SetFeatureValue("Intra-Peptide Euclidean Distance", 0.0);
                else
                {
                    List<double> singlePsmIntraPepEuDistLi = GetEuDistFromRatio(singlePsmPepRatioLi, singlePsmPepTotalRatioLi);
                    for (int i = 0; i < singlePsmPepNameLi.Count; i++)
                        this.dataContainerObj.dbSpstPsmInfoDic[singlePsmPepNameLi[i]].SetFeatureValue("Intra-Peptide Euclidean Distance",
                            singlePsmIntraPepEuDistLi[i]);
                }
            }
            return;
        }
       
        private void FilterDbSpstIproFile()
        {
            Console.WriteLine("Filtering database + spectraST iprophet file and writing to new iprophet...");
            //Xml reader setup
            XmlReaderSettings readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            XmlReader iproDbSpstReader = XmlReader.Create(String.Format("{0}{1}",this.mainDir,this.iproDbSpstFile), readerSettings);
            XmlReader msmsRunReader = iproDbSpstReader;
            iproDbSpstReader.Read(); //Jump to first node
            //Xml writer setup
            XmlWriterSettings writerSettings = new XmlWriterSettings {Indent = true, IndentChars = " "};
            XmlWriter modIproDbSpstWriter = XmlWriter.Create(String.Format("{0}{1}",this.mainDir, this.modIproDbSpstFile), writerSettings);
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
                    Console.WriteLine(String.Format("Warning: unexpected node in {0}: {1}", this.mainDir + this.iproDbSpstFile, iproDbSpstReader.Name));
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
        /// </summary>
        /// <param name="psmName">Name for the current PSM</param>
        /// <returns></returns>
        private bool FilterPsm(string psmName)
        {
            //If the PSM need not to be considered (FDR too small / shared peptide / missing value / etc.)
            if (!this.dataContainerObj.dbSpstPsmInfoDic.ContainsKey(psmName))
                return false;

            //Check whether the PSM is common (also in database search) or one of those added by spectraST
            if (!this.dataContainerObj.dbPsmNameLi.Contains(psmName))
                return false;

            //Filtering for every feature
            //Console.WriteLine(String.Format("{0}: added PSM", psmName));
            this.dataContainerObj.dbSpstPsmInfoDic.TryGetValue(psmName, out ds_PsmInfo psmInfoObj);
            foreach (KeyValuePair<string,string> featAndType in ds_Filter.featAndTypeDic)
            {
                var featValue = 0.0;
                //cast feature value to to correct data type
                if (featAndType.Value == "int")
                    featValue = (int)psmInfoObj.GetFeatureValue(featAndType.Key);
                else if (featAndType.Value == "double")
                    featValue = (double) psmInfoObj.GetFeatureValue(featAndType.Key);

                foreach ((double lowerLim, double upperLim) filtRange in this.filterParamObj.GetFiltRange(featAndType.Key))
                {
                    if ((featValue >= filtRange.lowerLim) && (featValue <= filtRange.upperLim))
                    {
                        //this.result.Add(psmName);
                        return true;
                    }
                }
            }      
            return false;
        }
        
    }
}

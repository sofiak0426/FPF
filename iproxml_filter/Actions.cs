using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using ResultReader;

namespace iproxml_filter
{
    //Define thread actions

    public class Actions
    {
        public readonly string[] paramNames = {
            "Database Iprophet Search File","Database + SpectraST Iprophet Search File","Output File",
            "Channel Number","Reference Channel","Decoy Prefix", "Charge", "Mass", "Peptide Length",
            "Intra-Peptide Euclidean Distance","Intra-Protein Euclidean Distance" };
        public readonly object infoLock = new object(); //For adding a psm to the info list

        //Global Variables
        public string mainDir;
        public string iproDbFile;
        public string iproDbSpstFile;
        public string modIproDbSpstFile;
        public int channelCnt;
        public int refChan;
        public string decoyPrefix;
        public float fdr001;

        //Data storage
        public ds_DataContainer dataContainerObj;
        public ds_PsmFilterParam filterListObj;

        /// <summary>
        /// Defines thread actions by specified id
        /// </summary>
        public string DoWorkerJobs(int id)
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
            return (String.Format("{0} Done", id));
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
            this.filterListObj = new ds_PsmFilterParam();
            this.ReadParamFile(this.mainDir + paramFile);

            List<int> workerIds = new List<int>{0,1};
            Parallel.ForEach(workerIds, workerId =>
            {
                string result = this.DoWorkerJobs(workerId);
                Console.WriteLine(result); //temporarily for testing
            });

            CalEuDist();
            
            this.FilterDbSpstIproFile();
            return;
        }

        /// <summary>
        /// Reads the parameter file line by line and modifies global variables.
        /// Checks whether the parameter name for each line corresponds to the list paramNames.
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

                string errorCode = String.Format("Parameter error:" +
                    "Have you modified the parameter {0} to \"{1}\"?",
                    paramNames[lineCnt - 1], lineElementsArr[0]);

                //parameter names does not match default
                if (lineElementsArr[0] != paramNames[lineCnt - 1])
                    throw new ApplicationException(errorCode);

                //if a feature filter is empty, skip the feature
                if (lineCnt >= 7 && String.IsNullOrEmpty(lineElementsArr[1]))
                    continue;

                switch (lineCnt)
                {
                    case 1: //Database Iprophet Search File:
                        this.iproDbFile = lineElementsArr[1];
                        break;
                    case 2: //"Database + SpectraST Iprophet Search File"
                        this.iproDbSpstFile = lineElementsArr[1];
                        break;
                    case 3:
                        this.modIproDbSpstFile = lineElementsArr[1];
                        break;
                    case 4: //Channel Number
                        int.TryParse(lineElementsArr[1], out this.channelCnt);
                        break;
                    case 5: //Reference Channel
                        int.TryParse(lineElementsArr[1], out this.refChan);
                        break;
                    case 6:
                        this.decoyPrefix = lineElementsArr[1];
                        break;
                    default: //for features
                        if(this.AddFilters(lineCnt, lineElementsArr[1]) == false)
                            throw new ApplicationException(errorCode);
                        break;
                }
            }
            paramFileReader.Close();
            //this.filterListObj.PrintFilter(); //testing
            return;
        }

        /// <summary>
        /// Adds filter ranges for each feature, as specified by the user in the param file, to filter list for further use.
        /// </summary>
        /// <param name="lineCnt"> Specify number of feature in the param name list</param>
        /// <param name="filterStr">The string containing ranges of filters, which is read from the param file</param>
        private bool AddFilters(int lineCnt, string filterStr)
        {
            String[] filterArr = filterStr.Split(',');
            foreach (string filter in filterArr)
            {
                if (filter.Trim(' ').IndexOf('-') == -1)
                    throw new ApplicationException(String.Format("Feature {0}: " +
                        "You entered in the wrong filter format", paramNames[lineCnt - 1]));

                String[] filtLimArr = filter.Trim(' ').Split('-');
                (double lowerLim, double upperLim) filtLim;
                if (filtLimArr[0] == String.Empty)
                    filtLim.lowerLim = Double.NegativeInfinity;
                else
                    double.TryParse(filtLimArr[0], out filtLim.lowerLim);
                if (filtLimArr[1] == String.Empty)
                    filtLim.upperLim = Double.PositiveInfinity;
                else
                    double.TryParse(filtLimArr[1], out filtLim.upperLim);

                if (!this.filterListObj.AddFilter(paramNames[lineCnt - 1], filtLim))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Parse PSM names that are in the database search iprophet file
        /// </summary>
        private void ReadIproDb()
        {
            using XmlReader IproDbReader = XmlReader.Create(this.mainDir + this.iproDbFile);
            while (IproDbReader.Read())
            {
                if (IproDbReader.NodeType != XmlNodeType.Element)
                    continue;
                if (IproDbReader.Name == "spectrum_query")
                    this.dataContainerObj.dbPsmNameLi.Add(IproDbReader.GetAttribute("spectrum"));
            }
            Console.WriteLine("Finished database search iprophet parsing: {0:G}", this.dataContainerObj.dbPsmNameLi.Count);
            return;
        }

        /// <summary>
        /// Parse database + spectraST serach iprophet file with search result reader
        /// </summary>
        private void ReadIproDbSpst()
        {
            PepXmlProtXmlReader iproDbSpstReader = new PepXmlProtXmlReader();
            this.dataContainerObj.iproDbSpstResult = iproDbSpstReader.ReadFiles(this.mainDir + this.iproDbSpstFile, "",
                XmlParser_Action.Read_PepXml, SearchResult_Source.TPP_PepXml);

            //get fdr < 1% probability
            this.fdr001 = dataContainerObj.iproDbSpstResult.GetPepMinProbForFDR(0.01f, "");
        }

        /// <summary>
        ///
        /// </summary>
        private void AddPsmInfo()
        {
            
        }

        private List<double> CalRatio(List<double> intenLi) //calculate channel error of psms
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
                double dist = 0;
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

        private void CalEuDist()
        {
            foreach (KeyValuePair<string, ds_Protein> prot in dataContainerObj.iproDbSpstResult.Protein_Dic)
            {
                //Lists for intra-protein euclidean distance 
                List<string> psmsInProtNameLi = new List<string>(); //Store all names of PSMs in this protein
                List<List<double>> psmsInProtRatioLi = new List<List<double>>(); //Store channel ratio of all PSMs in the protein
                List<double> psmsInProtTotalRatioLi = new List<double>(); //Store channel ratio sum of all PSMs in the protein for further use
                List<string> singlePsmPepNameLi = new List<string>(); //Store all names of PSMs in this protein
                List<List<double>> singlePsmPepRatioLi = new List<List<double>>(); //Store channel ratio of all PSMs in the protein
                List<double> singlePsmPepTotalRatioLi = new List<double>(); //Store channel ratio sum of all PSMs in the protein for further use
                for (int i = 0; i < this.channelCnt - 1; i++)
                {
                    psmsInProtTotalRatioLi.Add(0.0);
                    singlePsmPepTotalRatioLi.Add(0.0);
                }

                //Ignore decoy proteins
                if (prot.Value.ProtID.StartsWith(decoyPrefix))
                    continue;

                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    //Lists for intra-peptide euclidean distance
                    List<string> psmsInPepNameLi = new List<string>();
                    List<List<double>> psmsInPepRatioLi = new List<List<double>>();
                    List<double> psmsInPepTotalRatioLi = new List<double>(); //Store channel ratio sum of all PSMs in the peptide for further use
                    for (int i = 0; i < this.channelCnt - 1; i++)
                        psmsInPepTotalRatioLi.Add(0.0);

                    //Get psms that doesn't have a shared peptide
                    if (pep.Value.b_IsUnique == false)
                        continue;

                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        //Ignore psms with interprophet probability < 1% FDR probability
                        string keyScoreType = this.dataContainerObj.iproDbSpstResult.GetKeyScoreStr();
                        Dictionary<string, double> psmScoreDic = (Dictionary<string, double>)psm.Score;
                        double psmScore = 0;
                        if (psmScoreDic.ContainsKey(keyScoreType))
                            psmScore = psmScoreDic[keyScoreType];
                        else //search hits with no iprophet score
                            continue;
                        if (psmScore < this.fdr001)
                            continue;

                        //get intensity and ratio
                        List<double> psmIntenLi = new List<double>();
                        psmIntenLi.AddRange(psm.Libra_ChanIntenDi.Values);
                        if (psmIntenLi.Contains(0)) //Only get PSMs that has no missing intensity value
                            continue;
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
                            if (!this.dataContainerObj.psmInfoDic.ContainsKey(psmsInPepNameLi[i])) // if the PSM is not added to dic yet
                            {
                                lock (infoLock)
                                    this.dataContainerObj.psmInfoDic.Add(psmsInPepNameLi[i], new PsmInfo());
                            }
                            this.dataContainerObj.psmInfoDic[psmsInPepNameLi[i]].IntraPepEuDist = intraPepEuDistLi[i];
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
                    if (!this.dataContainerObj.psmInfoDic.ContainsKey(psmsInProtNameLi[i])) // if the PSM is not added to dic yet
                    {
                        lock(infoLock)
                            this.dataContainerObj.psmInfoDic.Add(psmsInProtNameLi[i], new PsmInfo());
                    }
                    this.dataContainerObj.psmInfoDic[psmsInProtNameLi[i]].IntraProtEuDist = intraProtEuDistLi[i];
                }

                //Calculate intra-peptide euclidean distance for peptides with only one PSM and store to dictionary
                if (singlePsmPepNameLi.Count == 0)
                    continue;
                else if (singlePsmPepNameLi.Count == 1)
                    this.dataContainerObj.psmInfoDic[singlePsmPepNameLi[0]].IntraPepEuDist = 0;
                else
                {
                    List<double> singlePsmIntraPepEuDistLi = GetEuDistFromRatio(singlePsmPepRatioLi, singlePsmPepTotalRatioLi);
                    for (int i = 0; i < singlePsmPepNameLi.Count; i++)
                        this.dataContainerObj.psmInfoDic[singlePsmPepNameLi[i]].IntraPepEuDist = singlePsmIntraPepEuDistLi[i];
                }
            }
            return;
        }
       
        private void FilterDbSpstIproFile()
        {
            //Xml reader setup
            XmlReaderSettings readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            XmlReader iproDbSpstReader = XmlReader.Create(this.mainDir + this.iproDbSpstFile, readerSettings);
            XmlReader msmsReader = iproDbSpstReader;
            //Xml writer setup
            XmlWriterSettings writerSettings = new XmlWriterSettings {Indent = true, IndentChars = " "};
            XmlWriter modIproDbSpstWriter = XmlWriter.Create(this.mainDir + this.modIproDbSpstFile, writerSettings);
            int i = 0;
            while (true)
            {
                /*
                if (i < 3)
                {
                    Console.Write(i);
                    Console.Write(iproDbSpstReader.Name);
                    Console.Write(iproDbSpstReader.NodeType);
                    Console.Write(iproDbSpstReader.AttributeCount);
                }
                i++;
                */
                Console.WriteLine("Start:"+iproDbSpstReader.Name);
                if (iproDbSpstReader.Name == "xml") //read xml header
                {
                    modIproDbSpstWriter.WriteNode(iproDbSpstReader, false);
                    continue;
                }
                else if (iproDbSpstReader.Name == "msms_pipeline_analysis") //read namespaces
                {
                    //Console.WriteLine(iproDbSpstReader.GetAttribute("date"));
                    //Console.WriteLine(iproDbSpstReader.GetAttribute("xmlns:xsi"));
                    //Console.WriteLine(iproDbSpstReader.GetAttribute("xsi:schemaLocation"));
                    //Console.WriteLine(iproDbSpstReader.GetAttribute("summary_xml"));
                    modIproDbSpstWriter.WriteStartElement(iproDbSpstReader.Name, iproDbSpstReader.GetAttribute("xmlns"));
                    modIproDbSpstWriter.WriteAttributeString("date", iproDbSpstReader.GetAttribute("date"));
                    modIproDbSpstWriter.WriteAttributeString("xsi", "schemaLocation", iproDbSpstReader.GetAttribute("xmlns:xsi"), iproDbSpstReader.GetAttribute("xsi:schemaLocation"));
                    modIproDbSpstWriter.WriteAttributeString("summary_xml", iproDbSpstReader.GetAttribute("summary_xml"));
                }
                else if (iproDbSpstReader.Name == "analysis_summary") //Other analysis summaries
                {
                    modIproDbSpstWriter.WriteNode(iproDbSpstReader, false);
                    continue;
                }
                else if (iproDbSpstReader.Name == "msms_run_summary") //Contain PSM information
                {
                    msmsReader = iproDbSpstReader.ReadSubtree();
                    ReadMsms(msmsReader, modIproDbSpstWriter);
                    //iproDbSpstReader.Skip();
                    //continue;
                    break; //for testing
                }
                else
                    Console.WriteLine(String.Format("Warning: unexpected node in {0}: {1}", this.mainDir + this.iproDbSpstFile, iproDbSpstReader.Name));
                iproDbSpstReader.Read();
                Console.WriteLine("End:" + iproDbSpstReader.Name);
                if (iproDbSpstReader.EOF == true)
                {
                    Console.WriteLine("Done!");
                    break;
                }
            }
            iproDbSpstReader.Close();
            msmsReader.Close();
            modIproDbSpstWriter.Close();
        }

        private void ReadMsms(XmlReader msmsReader, XmlWriter modIproDbSpstWriter)
        {
            //Reader setup
            XmlReaderSettings readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            msmsReader.Read(); //Jump to first node

            while(true)
            {
                if (msmsReader.Name == "msms_run_summary") //itself
                    msmsReader.Read();

                if (msmsReader.Name == "spectrum_query")
                {
                    //FilterPsm(); //Do filter spectrum
                    msmsReader.Skip();
                    continue;
                    //break; //for testing
                }
                
                if (msmsReader.EOF == true)
                {
                    Console.WriteLine("msms reading done!");
                    break;
                }
                else
                {
                    Console.WriteLine(msmsReader.Name);
                    modIproDbSpstWriter.WriteNode(msmsReader, false);
                }
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
            return false;
        }
        
    }
}

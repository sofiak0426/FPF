﻿using System;
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
        /// Defines thread action by id
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
                case 2:
                    this.CalIntraProtEuDist();
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
            
            //Calculate intra-pep and intra-prot euclidean distance for each PSM
            workerIds = new List<int> {2};
            Parallel.ForEach(workerIds, workerId =>
            {
                string result = this.DoWorkerJobs(workerId);
                Console.WriteLine(result); //temporarily for testing
            });
            
            //this.FilterDbSpstIproFile();
            return;
        }

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
        /// 
        /// </summary>
        /// <param name="lineCnt"> Specify number of feature in the param name list</param>
        /// <param name="filterStr">The string that contains features, read from param file</param>
        /// <param name="lineCnt"> Specify feature in the </param>
        /// <param name="filterStr"></param>
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

        private void ReadIproDbSpst()
        {
            PepXmlProtXmlReader iproDbSpstReader = new PepXmlProtXmlReader();
            this.dataContainerObj.iproDbSpstResult = iproDbSpstReader.ReadFiles(this.mainDir + this.iproDbSpstFile, "",
                XmlParser_Action.Read_PepXml, SearchResult_Source.TPP_PepXml);

            //get fdr < 1% probability
            this.fdr001 = dataContainerObj.iproDbSpstResult.GetPepMinProbForFDR(0.01f, "");
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

        private void CalIntraProtEuDist() //calculate intra-prot euclidean for each psm in db + spec file
        {
            using StreamWriter file = new StreamWriter("./eupsms.txt");
            foreach (KeyValuePair<string, ds_Protein> prot in dataContainerObj.iproDbSpstResult.Protein_Dic)
            {
                
                //Ignore decoy proteins
                if (prot.Value.ProtID.StartsWith(decoyPrefix))
                    continue;
                
                //get all ratios of PSMs in this protein
                List<string> psmNameLi = new List<string>();
                List<List<double>> allPsmsRatioLi = new List<List<double>>();
                List<double> totalRatioLi = new List<double>(); //Store total intensity of all PSMs in the protein for further use
                for (int i = 0; i < channelCnt - 1; i++)
                    totalRatioLi.Add(0.0);

                foreach (KeyValuePair <string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    //Get psms that doesn't have a shared peptide
                    if (pep.Value.b_IsUnique == false)
                    {
                        continue;
                    }

                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        //Get psms with interprophet probability FDR > 1%
                        //Console.WriteLine(psm.QueryNumber);
                        string keyScoreType = this.dataContainerObj.iproDbSpstResult.GetKeyScoreStr();
                        Dictionary<string, double> psmScoreDic = (Dictionary<string, double>)psm.Score;
                        double psmScore = 0;
                        if (psmScoreDic.ContainsKey(keyScoreType))
                            psmScore = psmScoreDic[keyScoreType];
                        else //search hits with no iprophet score
                            continue;
                        if (psmScore < this.fdr001)
                            continue;
                        
                        //get intensity
                        List<double> intenLi = new List<double>();
                        intenLi.AddRange(psm.Libra_ChanIntenDi.Values);
                        if (intenLi.Contains(0)) //Only get PSMs that has no missing intensity value
                            continue;
                        List<double> ratioLi = CalRatio(intenLi);
                        psmNameLi.Add(psm.QueryNumber);
                        allPsmsRatioLi.Add(ratioLi);
                        //Add to total intensity
                        for (int i = 0; i < channelCnt - 1; i++)
                            totalRatioLi[i] += ratioLi[i];
                        
                    }
                }
                /*
                Console.WriteLine(prot.Key + ":");
                foreach (string name in psmNameLi)
                    Console.Write(name + ",");
                Console.Write("\n");
                */
                
                int psmCountInProt = allPsmsRatioLi.Count;
                if (psmCountInProt == 1) //If only one PSM, set euclidean distance to 0
                {
                    this.dataContainerObj.intraProtEuDistDic.Add(psmNameLi[0], 0);
                    continue;
                }

                //Store average ratio for each channel for all PSMs in protein
                List<double> avgRatioLi = new List<double>();
                for (int i = 0; i < channelCnt - 1; i++)
                    avgRatioLi.Add(totalRatioLi[i] / psmCountInProt);

                //calculate euclidean
                for (int i = 0; i < psmNameLi.Count; i++)
                {
                    double dist = 0;
                    for (int j = 0; j < channelCnt - 1; j++)//For each channel
                    {
                        //avgOtherRatio: For a single channel, avg ratio of other PSMs (except the current PSM) in this protein
                        double avgOtherRatio = (totalRatioLi[j] - allPsmsRatioLi[i][j]) / (psmCountInProt - 1);
                        double d = Math.Abs((allPsmsRatioLi[i][j] - avgOtherRatio) / avgRatioLi[j]);
                        dist += Math.Pow(d, 2);
                    }
                    dist = Math.Sqrt(dist);

                    //Add the PSM's euclidean distance to Dictionary
                    this.dataContainerObj.intraProtEuDistDic.Add(psmNameLi[i], dist);
                    Console.WriteLine(psmNameLi[i]);
                    file.WriteLine(psmNameLi[i]);
                }
                
            }
            return;
        }
       
        private void FilterDbSpstIproFile()
        {
            XmlReaderSettings readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            XmlReader iproDbSpstReader = XmlReader.Create(this.mainDir + this.iproDbSpstFile, readerSettings);
            //Xml writer settings
            XmlWriterSettings writerSettings = new XmlWriterSettings {Indent = true, IndentChars = "\t"};
            XmlWriter modIproDbSpstWriter = XmlWriter.Create(this.mainDir + this.modIproDbSpstFile, writerSettings);
            int i = 0;
            while (true)
            {
                if (i < 3)
                {
                    Console.WriteLine(i);
                    Console.WriteLine(iproDbSpstReader.Name);
                    Console.WriteLine(iproDbSpstReader.NodeType);
                    Console.WriteLine(iproDbSpstReader.AttributeCount);
                }
                i++;

                if (iproDbSpstReader.Name == "xml") //read xml header
                {
                    modIproDbSpstWriter.WriteNode(iproDbSpstReader, false);
                    continue;
                }
                else if (iproDbSpstReader.Name == "msms_pipeline_analysis") //read namespaces
                {
                    Console.WriteLine(iproDbSpstReader.GetAttribute("date"));
                    Console.WriteLine(iproDbSpstReader.GetAttribute("xmlns:xsi"));
                    Console.WriteLine(iproDbSpstReader.GetAttribute("xsi:schemaLocation"));
                    Console.WriteLine(iproDbSpstReader.GetAttribute("summary_xml"));
                    modIproDbSpstWriter.WriteStartElement(iproDbSpstReader.Name, iproDbSpstReader.GetAttribute("xmlns"));
                    modIproDbSpstWriter.WriteAttributeString("date", iproDbSpstReader.GetAttribute("date"));
                    //modIproDbSpstWriter.WriteAttributeString("xmlns", "xsi", null, iproDbSpstReader.GetAttribute("xmlns:xsi")); //needs to be checked
                    modIproDbSpstWriter.WriteAttributeString("xsi", "schemaLocation", iproDbSpstReader.GetAttribute("xmlns:xsi"), iproDbSpstReader.GetAttribute("xsi:schemaLocation"));
                    modIproDbSpstWriter.WriteAttributeString("summary_xml", iproDbSpstReader.GetAttribute("summary_xml"));
                }
                /*
                else if (iproDbSpstReader.Name != "msms_run_summary")
                {
                    Console.WriteLine(iproDbSpstReader.Name);
                    modIproDbSpstWriter.WriteNode(iproDbSpstReader, false);
                    break;
                    //continue;
                }
                */
                Console.WriteLine(iproDbSpstReader.Name) ;
                iproDbSpstReader.Read();
            }
            modIproDbSpstWriter.Close();
        }

        private void ReadMsms(XmlReader msmsReader, XmlWriter modIproDbSpstWriter)
        {
            int i = 0;
            while (true)
            {
                
                if (i < 7)
                {
                    Console.WriteLine(i);
                    Console.WriteLine(msmsReader.Name);
                    Console.WriteLine(msmsReader.NodeType);
                    Console.WriteLine(msmsReader.AttributeCount);
                }
                i++;

                if (msmsReader.Name == "msms_pipeline_analysis")
                    continue;
                /*
                if (msmsReader.Name == "msms_run_summary")
                {
                    //get PSM spectrum query
                    break;
                }
                else if (msmsReader.Name != "")
                {
                    Console.WriteLine(msmsReader.Name);
                    modIproDbSpstWriter.WriteNode(msmsReader, false);
                    continue;
                }
                else
                */
                Console.WriteLine(msmsReader.NodeType);
                msmsReader.Read();

            }
            msmsReader.Close();
        }
        
    }
}

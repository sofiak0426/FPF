using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Diagnostics;

namespace ResultReader
{
    public class PepXmlProtXmlReader 
    {
        ds_SearchResult searchResultObj = new ds_SearchResult();
        XmlParser_Action Cmd = new XmlParser_Action();

        /// <summary>
        /// Parse_TPP: one fileName you want to Parse plz put in first Param, second file put into second slot if needed
        /// When Command is Read_TPP_PepProphet_Mayu, Parser doesn't save prob of Peptide in every PSM.
        /// </summary>
        /// <param name="pepLv_pepXml">Result file's Name(pepXML file) after peptide prophet/protein prophet</param>
        /// <param name="protLv_protXml">Result file's Name(protXML file) is after protein prophet</param>
        /// <param name="Command">XmlParser_Action: Parse Read_PepXml=0/Read_ProtXml=1/Read_PepAndProtXml=2 </param>
        /// <param name="SourceType">Xml_Source: Search Result from different Sources</param>
        /// <returns>ds_SearchResult</returns>
        public ds_SearchResult ReadFiles(string pepLv_pepXml, string protLv_protXml, XmlParser_Action Command, SearchResult_Source SourceType)
        {
            this.Cmd = Command;
            this.searchResultObj.Source = SourceType;

            switch (this.Cmd)
            {
                case XmlParser_Action.Read_PepXml:
                    if (pepLv_pepXml != "")
                    {
                        this.ReadPepXml(pepLv_pepXml);
                        this.searchResultObj.RefreshPepProt_Dic();
                        if(Command == XmlParser_Action.Read_PepXml)
                            this.searchResultObj.RefreshPepProtGroup_Dic();
                        this.searchResultObj.DeterminePeptideUniqueness();
                    }
                    break;
                case XmlParser_Action.Read_ProtXml:
                    if (protLv_protXml != "")
                        this.ReadProtXml(protLv_protXml);
                    break;
                case XmlParser_Action.Read_PepAndProtXml:
                    if ((pepLv_pepXml != "") && (protLv_protXml != ""))
                    {
                        this.ReadPepXml(pepLv_pepXml);
                        this.ReadProtXml(protLv_protXml);
                        this.searchResultObj.RefreshPepProt_Dic();
                        this.searchResultObj.RefreshPepProtGroup_Dic();
                        this.searchResultObj.DeterminePeptideUniqueness();
                    }
                    break;
                default: 
                    break;
            }
            return this.searchResultObj;
        }


        private void ReadPepXml(string pepXml) //read "peptide" prophet and interprophet in TPP
        {      
            //read files
            XmlReader pepXmlReader = XmlReader.Create(pepXml);

            while (pepXmlReader.Read())
            {
                if(pepXmlReader.NodeType != XmlNodeType.Element)
                    continue;

                switch(pepXmlReader.Name)
                {
                    case "search_summary":
                        XmlReader pepXmlInnerReaderLv1 = pepXmlReader.ReadSubtree(); //limit range to its subtree
                        pepXmlInnerReaderLv1.MoveToContent();
                        this.PepXML_getBasicInfo(pepXmlInnerReaderLv1);
                        //---get basic information, also includes mod Information---// 
                        pepXmlInnerReaderLv1.Close();
                        break;

                    case "roc_error_data":                       
                        pepXmlInnerReaderLv1 = pepXmlReader.ReadSubtree();
                        pepXmlInnerReaderLv1.MoveToContent();
                        this.PepXML_getProbTableInfo(pepXmlInnerReaderLv1);
                        // ---get probability table information---//
                        pepXmlInnerReaderLv1.Close();
                        break;

                    case "spectrum_query":
                        pepXmlInnerReaderLv1 = pepXmlReader.ReadSubtree();
                        pepXmlInnerReaderLv1.MoveToContent();
                        this.PepXML_getProtPepPsmInfo(pepXmlInnerReaderLv1);
                        // ---get psm, protein and peptide information---//
                        pepXmlInnerReaderLv1.Close();
                        break;

                    case "libra_summary":
                        pepXmlInnerReaderLv1 = pepXmlReader.ReadSubtree();
                        pepXmlInnerReaderLv1.MoveToContent();
                        this.GetLibraChannelMz(pepXmlInnerReaderLv1);

                        pepXmlInnerReaderLv1.Close();
                        break;

                    default:
                        break;        
                }
  
            }
            pepXmlReader.Close(); //close reader
        }

        private void ReadProtXml(string protXml) //read "peptide" prophet and interprophet in TPP
        {
            //read files
            XmlReader protXmlReader = XmlReader.Create(protXml);

            while (protXmlReader.Read())
            {
                if (protXmlReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (protXmlReader.Name)
                {
                    case "proteinprophet_details":
                        XmlReader protXmlInnerReaderLv1 = protXmlReader.ReadSubtree(); //limit range to its subtree
                        protXmlInnerReaderLv1.MoveToContent();
                        this.ProtXML_getProbTableInfo(protXmlInnerReaderLv1);
                        // ---get probability table information---//
                        protXmlInnerReaderLv1.Close();
                        break;

                    case "protein_group":
                        protXmlInnerReaderLv1 = protXmlReader.ReadSubtree(); //limit range to its subtree
                        protXmlInnerReaderLv1.MoveToContent();
                        this.ProtXML_getProtInfo(protXmlInnerReaderLv1);
                        // ---get protein information---//
                        protXmlInnerReaderLv1.Close();
                        break;

                    default:
                        break;
                }
            }
            protXmlReader.Close();
        }

        /// <summary>
        /// get Basic Information from pepXML
        /// </summary>
        private void PepXML_getBasicInfo(XmlReader BasicInfoReader)
        {
            ds_BasicInfo BasicInfoObj = new ds_BasicInfo();

            if (this.searchResultObj.Source == SearchResult_Source.PD_PepXml && !BasicInfoReader.HasAttributes)
                return; //PD first info is only txt workflow log(just SKIP)

            BasicInfoObj.filePath = (BasicInfoReader.GetAttribute("base_name") != null) ? Path.GetFileName(BasicInfoReader.GetAttribute("base_name")) : "";
            BasicInfoObj.DB = (BasicInfoReader.GetAttribute("search_engine") != null) ? BasicInfoReader.GetAttribute("search_engine") : "";  //rough info

            while (BasicInfoReader.Read()) //search every Nodes "search_summary"
            {
                if (BasicInfoReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (BasicInfoReader.Name)
                {
                    case "search_database":    //childNodeLv1 is search_database
                        BasicInfoObj.FastaVer = (BasicInfoReader.GetAttribute("local_path") != null) ? Path.GetFileName(BasicInfoReader.GetAttribute("local_path")) : "";
                        break;

                    case "aminoacid_modification":
                    case "terminal_modification":
                        this.Read_modInfo(BasicInfoReader, BasicInfoObj);
                        // ---get modification information---//
                        break;

                    case "parameter": //childNodeLv1 is parameter
                        if ((BasicInfoReader.GetAttribute("name") == null) || (BasicInfoReader.GetAttribute("name") == null))
                            break;

                        switch (BasicInfoReader.GetAttribute("name"))
                        {
                            case "TOL":
                            case "TOLU":
                            case "spectrum, parent monoisotopic mass error plus":
                            case "spectrum, parent monoisotopic mass error units":
                            case "PeptideTolerance":
                                BasicInfoObj.msTol += BasicInfoReader.GetAttribute("value");
                                break;

                            case "ITOL":
                            case "ITOLU":
                            case "spectrum, fragment monoisotopic mass error":
                            case "spectrum, fragment monoisotopic mass error units":
                            case "FragmentTolerance":
                                BasicInfoObj.msmsTol += BasicInfoReader.GetAttribute("value");
                                break;

                            case "DB":
                                BasicInfoObj.DB = BasicInfoReader.GetAttribute("value"); //if exist, detail info
                                BasicInfoObj.Version = BasicInfoReader.GetAttribute("value");
                                break;

                            case "TAXONOMY":
                            case "list path, taxonomy information":
                                BasicInfoObj.taxonomy = BasicInfoReader.GetAttribute("value");
                                break;

                            case "INSTRUMENT":
                                BasicInfoObj.Instrument = BasicInfoReader.GetAttribute("value");
                                break;
                            case "FastaDatabase":
                                BasicInfoObj.FastaVer = BasicInfoReader.GetAttribute("value");
                                break;

                            default:
                                break;
                        }
                        break;

                    default:
                        break;
                }
            }//end parse child Nodes "search_summary"

            this.searchResultObj.BasicInfoList.Add(BasicInfoObj);
        }

        /// <summary>
        /// get Probability Table Information from pepXML
        /// </summary>
        private void PepXML_getProbTableInfo(XmlReader ProbTableReader)
        {
            XmlReader innerProbTableReaderLv1 = ProbTableReader.ReadSubtree(); // get "roc_data_point" ChildNodes
            innerProbTableReaderLv1.MoveToContent();
            ds_ProbabilityTable ProbTableObj = new ds_ProbabilityTable();
            ProbTableObj.Charge = (innerProbTableReaderLv1.GetAttribute("charge") != null) ? innerProbTableReaderLv1.GetAttribute("charge") : "";

            while (innerProbTableReaderLv1.Read())
            {
                if (innerProbTableReaderLv1.NodeType != XmlNodeType.Element)
                    continue;
                if (innerProbTableReaderLv1.Name != "roc_data_point")    //childnodeLv1: roc_data_point
                    continue;
                ds_ProbToFDR ProbToFDRObj = new ds_ProbToFDR();
                ProbToFDRObj.Min_prob = (innerProbTableReaderLv1.GetAttribute("min_prob") != null) ? float.Parse(innerProbTableReaderLv1.GetAttribute("min_prob")) : 0;
                ProbToFDRObj.FDR_rate = (innerProbTableReaderLv1.GetAttribute("error") != null) ? float.Parse(innerProbTableReaderLv1.GetAttribute("error")) : 0;
                ProbTableObj.ProbToFDRlist.Add(ProbToFDRObj);
            } //end_roc_data_point

            this.searchResultObj.PepProphetProb_Dic.Add(ProbTableObj.Charge, ProbTableObj);

        }

        /// <summary>
        /// get Modification Information from pepXML
        /// </summary>
        private void Read_modInfo(XmlReader modInfoReader, ds_BasicInfo BasicInfoObj)
        {
            if (modInfoReader.GetAttribute("variable") == null)
                return; //protct

            if (modInfoReader.GetAttribute("variable") == "Y")
                this.Read_variable_modification(modInfoReader, BasicInfoObj);  //Read variable_modification
            else
                this.Read_fixed_modification(modInfoReader);  //Read fixed_modification
        }

        private void Read_variable_modification(XmlReader modInfoReader, ds_BasicInfo BasicInfoObj)
        {
            ModificationPack modpack = new ModificationPack();
            string modType ="";

            if (modInfoReader.Name == "terminal_modification") //aminoacid or terminal modification
                modType = "terminus";
            else
                modType = "aminoacid";

            if ((modInfoReader.GetAttribute(modType) == null) || (modInfoReader.GetAttribute("mass") == null))
                return;

            if (modType != "terminus")
                modpack.ModCode = modInfoReader.GetAttribute(modType) + "_" + (Math.Round(double.Parse(modInfoReader.GetAttribute("mass")), 3, MidpointRounding.AwayFromZero)).ToString();
            else
                modpack.ModCode = (modInfoReader.GetAttribute(modType)).ToLower() + "_" + (Math.Round(double.Parse(modInfoReader.GetAttribute("mass")), 3, MidpointRounding.AwayFromZero)).ToString();
            
            modpack.Identifier = searchResultObj.VarMod_Dic.Count; //assign order by parser
            modpack.Mass_diff = (modInfoReader.GetAttribute("massdiff") != null) ? double.Parse(modInfoReader.GetAttribute("massdiff")) : 0.0;
            //modpack.NeupackList  TBD (have not seen in TPP)

            // no need to add the same modification in interPeptideProphet
            if (!this.searchResultObj.VarMod_Dic.ContainsKey(modpack.ModCode))
            {
                this.searchResultObj.VarMod_Dic.Add(modpack.ModCode, modpack);
            }

            BasicInfoObj.searchPTM += (modpack.ModCode + " ,");
        }

        private void Read_fixed_modification(XmlReader modInfoReader)
        {
            ModificationPack modpack = new ModificationPack();
            string modType = "";

            if (modInfoReader.Name == "terminal_modification") //aminoacid or terminal modification
                modType = "terminus";
            else
                modType = "aminoacid";

            if ((modInfoReader.GetAttribute(modType) == null) || (modInfoReader.GetAttribute("mass") == null))
                return;

            modpack.ModCode = modInfoReader.GetAttribute(modType) + "_" + (Math.Round(double.Parse(modInfoReader.GetAttribute("mass")), 3, MidpointRounding.AwayFromZero)).ToString();
            
            modpack.Identifier = searchResultObj.FixedMod_Dic.Count;
            modpack.Mass_diff = (modInfoReader.GetAttribute("massdiff") != null) ? double.Parse(modInfoReader.GetAttribute("massdiff")) : 0.0;
            //modpack.NeupackList  TBD (have not seen in TPP)

            // no need to add the same modification in interPeptideProphet
            if (!this.searchResultObj.FixedMod_Dic.ContainsKey(modpack.ModCode))
                this.searchResultObj.FixedMod_Dic.Add(modpack.ModCode, modpack);
        }

        /// <summary>
        /// read Protein and Peptide data  (LEVEL: PSM > peptide > protein)
        /// </summary>
        private void PepXML_getProtPepPsmInfo(XmlReader queryXmlReader)
        {
            ds_PSM shared_psmObj = new ds_PSM(this.searchResultObj.Source);    //a query share some PSM parameters
            this.Read_PsmInfo(queryXmlReader, shared_psmObj);     // 20170509 從<spectrum_query (#$&)#(*& >取得一些資訊放到shared_psmObj，作為所有search_hits的psmObj的基礎

            // get "search_hit" Node     // 20170509 僅在找到search_hit時開始動作
            while (queryXmlReader.Read())
            {
                if (queryXmlReader.NodeType != XmlNodeType.Element)
                    continue;

                if (queryXmlReader.Name != "search_hit")    //innerReaderLv1: search_result
                    continue;

                ds_PSM psmObj = ds_PSM.DeepClone<ds_PSM>(shared_psmObj);    //local parameter for each peptide
                ds_Protein protObj = new ds_Protein();
                ds_Peptide pepObj = new ds_Peptide();

                this.Read_peptideInfo(queryXmlReader, pepObj, psmObj, protObj); //read protein and peptide
                if (psmObj.Rank > 1)
                    continue;

                //this.Interact_calcInfo(pepObj, psmObj);

                //copy Objs if alternative protein exists in pepXML
                List<ds_Protein> dupeProtObjs = new List<ds_Protein>();
                List<ds_Peptide> dupePepObjs = new List<ds_Peptide>();
                List<ds_PSM> dupePsmObjs = new List<ds_PSM>();
                if(protObj.AlterProtlist.Count > 0)
                {
                    foreach (string AlterProtName in protObj.AlterProtlist)
                    {
                        ds_Protein dupeProtObj = ds_Protein.DeepClone<ds_Protein>(protObj);
                        ds_Peptide dupePepObj = ds_Peptide.DeepClone<ds_Peptide>(pepObj);
                        ds_PSM dupePsmObj = ds_PSM.DeepClone<ds_PSM>(psmObj);
                        dupeProtObjs.Add(dupeProtObj);
                        dupePepObjs.Add(dupePepObj);
                        dupePsmObjs.Add(dupePsmObj);
                    }
                }

                this.AttachInfo_to_terminationDS(protObj, pepObj, psmObj);

                //if any alternative protein exists in pepXML
                if (protObj.AlterProtlist.Count > 0)
                {
                    int AlterProtNum = dupeProtObjs.Count;
                    int i = 0;
                    foreach (string AlterProtName in protObj.AlterProtlist)
                    {
                        ds_Protein AltprotObj = dupeProtObjs[i];
                        ds_Peptide DupePepObj = dupePepObjs[i];
                        ds_PSM DupePsmObj = dupePsmObjs[i];
                        AltprotObj.AlterProtlist.Add(AltprotObj.ProtID);
                        AltprotObj.ProtID = AlterProtName; //exchange Prot name betwwen AccNo and Prot Name in AlterList 
                        AltprotObj.AlterProtlist.Remove(AlterProtName);
                        this.AttachInfo_to_terminationDS(AltprotObj, DupePepObj, DupePsmObj);
                        i++;
                    }
                }
            }   //[end] every "search_hit" Node
        }

        //read peptide data
        private void Read_peptideInfo(XmlReader pepReader, ds_Peptide pepObj, ds_PSM psmObj, ds_Protein protObj)
        {
            psmObj.Rank = (pepReader.GetAttribute("hit_rank") != null) ? int.Parse(pepReader.GetAttribute("hit_rank")) : 0;
            pepObj.PrevAA = (pepReader.GetAttribute("peptide_prev_aa") != null) ? pepReader.GetAttribute("peptide_prev_aa") : "";
            pepObj.Sequence = (pepReader.GetAttribute("peptide") != null) ? pepReader.GetAttribute("peptide") : "";
            pepObj.NextAA = (pepReader.GetAttribute("peptide_next_aa") != null) ? pepReader.GetAttribute("peptide_next_aa") : "";
            psmObj.MissedCleavage = (pepReader.GetAttribute("num_missed_cleavages") != null) ? int.Parse(pepReader.GetAttribute("num_missed_cleavages")) : 0;
            pepObj.Theoretical_mass = (pepReader.GetAttribute("calc_neutral_pep_mass") != null) ? double.Parse(pepReader.GetAttribute("calc_neutral_pep_mass")) : 0.0;
            protObj.ProtID = (pepReader.GetAttribute("protein") != null) ? pepReader.GetAttribute("protein") : "";
            protObj.Description = (pepReader.GetAttribute("protein_descr") != null) ? pepReader.GetAttribute("protein_descr") : "";

            psmObj.MassError = (pepReader.GetAttribute("massdiff") != null) ? double.Parse(pepReader.GetAttribute("massdiff")) : 0;  // 20190124 直接從massdiff取得MassError

            if (psmObj.Rank > 1)
                return;

            XmlReader innerReaderLv1 = pepReader.ReadSubtree();
            innerReaderLv1.MoveToContent();
            Dictionary<string, double> scoreDic = new Dictionary<string, double>(); //key: Score type, value: Score value

            if (this.searchResultObj.Source != SearchResult_Source.Mayu) //initial score Object
                psmObj.Score = scoreDic;

            while (innerReaderLv1.Read())
            {
                if (innerReaderLv1.NodeType != XmlNodeType.Element)
                    continue;

                string scoreType = "";
                double scoreValue = -1.0F;

                //get other attributes in innerReaderLv1 nodes
                switch (innerReaderLv1.Name)
                {
                    case "modification_info":
                        pepObj.ModifiedSequence = (innerReaderLv1.GetAttribute("modified_peptide") != null) ? innerReaderLv1.GetAttribute("modified_peptide") : "";
                        if (innerReaderLv1.GetAttribute("mod_nterm_mass") != null)
                        {
                            ds_ModPosInfo Mod_ntermObj = new ds_ModPosInfo();
                            Mod_ntermObj.ModMass = Math.Round(double.Parse(innerReaderLv1.GetAttribute("mod_nterm_mass")), 3, MidpointRounding.AwayFromZero);
                            Mod_ntermObj.ModPos = 0;
                            pepObj.ModPosList.Add(Mod_ntermObj);
                        }
                        break;

                    case "mod_aminoacid_mass":
                        ds_ModPosInfo ModPosInfoObj = new ds_ModPosInfo();
                        ModPosInfoObj.ModPos = (innerReaderLv1.GetAttribute("position") != null) ? int.Parse(innerReaderLv1.GetAttribute("position")) : 0;
                        ModPosInfoObj.ModMass = (innerReaderLv1.GetAttribute("mass") != null) ? Math.Round(double.Parse(innerReaderLv1.GetAttribute("mass")), 3, MidpointRounding.AwayFromZero) : 0.0;
                        pepObj.ModPosList.Add(ModPosInfoObj);
                        break;

                    case "search_score":
                        if (innerReaderLv1.GetAttribute("name") == null)
                            break; //protect break

                        //score Value(一定要是數值, 否則為-1) Patch 0819 by Hsu
                        scoreType = innerReaderLv1.GetAttribute("name");
                        double bb = -1.0;
                        if (innerReaderLv1.GetAttribute("value") != null)
                            scoreValue = (double.TryParse(innerReaderLv1.GetAttribute("value"), out bb)) ? double.Parse(innerReaderLv1.GetAttribute("value")) : -1.0;
                        else
                            scoreValue = bb;

                        if (innerReaderLv1.GetAttribute("name") == "expect")
                            psmObj.ExpectValue = scoreValue;

                        switch (this.searchResultObj.Source)
                        {
                            case SearchResult_Source.Comet_PepXml:      //KeyScore: expect
                            case SearchResult_Source.MSGF_PepXml:       //KeyScore: SpecEValue
                            case SearchResult_Source.Myrimatch_PepXml:  //KeyScore: mvh
                            case SearchResult_Source.PD_PepXml:         //KeyScore: XCorr
                            case SearchResult_Source.Mascot_PepXml:     //KeyScore: ionscore
                            case SearchResult_Source.XTandem_PepXml:    //KeyScore: hyperscore
                            case SearchResult_Source.TPP_PepXml:        //KeyScore: peptideprophet_result
                            case SearchResult_Source.PD_Percolator_PepXml:
                                if (!scoreDic.ContainsKey(scoreType))  //protect
                                    scoreDic.Add(scoreType, scoreValue);
                                break;

                            case SearchResult_Source.Mayu:  //meaningful Petide's Prob is obtain from Mayu csv
                            default:
                                break;
                        }
                        break;


                    case "search_score_summary":
                        XmlReader searchScoreSummaryReader = innerReaderLv1.ReadSubtree();
                        searchScoreSummaryReader.MoveToContent();
                        while (searchScoreSummaryReader.Read())
                        {
                            if (searchScoreSummaryReader.NodeType != XmlNodeType.Element || searchScoreSummaryReader.Name != "parameter" || searchScoreSummaryReader.GetAttribute("name") == null)
                                continue;

                            scoreType = searchScoreSummaryReader.GetAttribute("name");
                            scoreValue = (searchScoreSummaryReader.GetAttribute("value") != null) ? double.Parse(searchScoreSummaryReader.GetAttribute("value")) : -1.0;
                            if (!scoreDic.ContainsKey(scoreType))
                                scoreDic.Add(scoreType, scoreValue);
                            else
                                scoreDic[scoreType] = scoreValue;                                                           
                        }

                        break;

                    case "peptideprophet_result":
                    case "interprophet_result":
                        scoreType = "peptideprophet_result";
                        scoreValue = -1.0;
                        scoreValue = (innerReaderLv1.GetAttribute("probability") != null) ? double.Parse(innerReaderLv1.GetAttribute("probability")) : -1.0;
                        if (this.searchResultObj.Source == SearchResult_Source.Mayu)
                            psmObj.Score = -1.0; //meaningful Petide's Prob is obtain from Mayu csv
                        else
                            if (!scoreDic.ContainsKey(scoreType))
                                scoreDic.Add(scoreType, scoreValue);
                            else if (innerReaderLv1.Name == "interprophet_result") //interprophet_result is more meaningful
                                scoreDic[scoreType] = scoreValue;
                        break;

                    case "percolator_result":
                        scoreType = "percolator_result";
                        scoreValue = -1.0;
                        scoreValue = (innerReaderLv1.GetAttribute("probability") != null) ? double.Parse(innerReaderLv1.GetAttribute("probability")) : -1.0;
                        if (!scoreDic.ContainsKey(scoreType))
                            scoreDic.Add(scoreType, scoreValue);
                        break;

                    case "alternative_protein":
                        string AlterProtName = (innerReaderLv1.GetAttribute("protein") != null) ? innerReaderLv1.GetAttribute("protein") : "";
                        protObj.AlterProtlist.Add(AlterProtName);
                        break;

                    case "libra_result":
                        int channelNumber;
                        double libra_intensity;
                        XmlReader libraReader = innerReaderLv1.ReadSubtree();
                        while (libraReader.Read())
                        {
                            if (libraReader.NodeType != XmlNodeType.Element)
                                continue;

                            if (libraReader.Name == "intensity")
                            {
                                if (int.TryParse(libraReader.GetAttribute("channel"), out channelNumber) && double.TryParse(libraReader.GetAttribute("normalized"), out libra_intensity))
                                    psmObj.libra_ChanIntenDi.Add(channelNumber, libra_intensity);
                            }                  
                        }
                        break;

                    default:
                        break;

                } //end innerReaderLv1 case
            } //end whileloop for innerReader

            if (pepObj.ModPosList.Count != 0)  //Use Lab's ModifiedSequence naming style in PepXml Parser
                pepObj.ModifiedSequence = this.TranslateModPosToModSeq(pepObj);  //handle Myrimatch_PepXml is without ModifiedSequence but with ModPosList.  
        }

        private void Read_PsmInfo(XmlReader psmReader, ds_PSM psmObj)
        {
            psmObj.QueryNumber = (psmReader.GetAttribute("spectrum") != null) ? psmReader.GetAttribute("spectrum"): "";
            TitleParser tt = new TitleParser();
            ds_ScanTitleInfo titleObj = tt.parse(psmObj.QueryNumber);
            psmObj.rawDataFileName = titleObj.rawDataName;
            psmObj.scanNumber = titleObj.scanNum;
            psmObj.SPCE = titleObj.SPCE;
            psmObj.Charge = (psmReader.GetAttribute("assumed_charge") != null) ? int.Parse(psmReader.GetAttribute("assumed_charge")) : 0;
            psmObj.Pep_exp_mass = (psmReader.GetAttribute("precursor_neutral_mass") != null) ? double.Parse(psmReader.GetAttribute("precursor_neutral_mass")) : 0.0;
            psmObj.Precursor_mz = (double)((psmObj.Pep_exp_mass + psmObj.Charge * 1.007276) / psmObj.Charge);
            psmObj.ElutionTime = (psmReader.GetAttribute("retention_time_sec") != null) ? float.Parse(psmReader.GetAttribute("retention_time_sec")) : 0;
        }

        private void Interact_calcInfo(ds_Peptide pepObj, ds_PSM psmObj)
        {
            psmObj.MassError = pepObj.Theoretical_mass - psmObj.Pep_exp_mass;
        }

        private void AttachInfo_to_terminationDS(ds_Protein protObj, ds_Peptide pepObj, ds_PSM psmObj)
        {
            ds_Protein temp_protObj = new ds_Protein(); // use it to read existed object in Prot_dictionary  if need
            ds_Peptide temp_pepObj = new ds_Peptide(); // use it to read existed object in Peptide_dictionary  if need
            string pepObj_Index = "";  //avoid no modification in peptide

            if (pepObj.ModifiedSequence == "")  //not modification in peptide
                pepObj_Index = pepObj.Sequence;
            else
                pepObj_Index = pepObj.ModifiedSequence;

            //attach data to termination DS:  protein > peptide > psm
            //handle for repeat index in dictionary:  
            if (this.searchResultObj.Protein_Dic.ContainsKey(protObj.ProtID) &&
                this.searchResultObj.Protein_Dic[protObj.ProtID].Peptide_Dic.ContainsKey(pepObj_Index))
            {   //case1. same protein exists  && same peptide exists
                temp_pepObj = this.searchResultObj.Protein_Dic[protObj.ProtID].Peptide_Dic[pepObj_Index];
                bool is_PSMObjExist = false;  //check PSM是否重複?
                foreach (ds_PSM pObj in temp_pepObj.PsmList)
                {
                    if ((pObj.scanNumber == psmObj.scanNumber) && (pObj.rawDataFileName == psmObj.rawDataFileName))
                        is_PSMObjExist = true;
                }
                if (!is_PSMObjExist)
                    temp_pepObj.PsmList.Add(psmObj);
            }
            else if (this.searchResultObj.Protein_Dic.ContainsKey(protObj.ProtID))
            {    //case2. only same protein exists
                temp_protObj = this.searchResultObj.Protein_Dic[protObj.ProtID];
                bool is_PSMObjExist = false;  //check PSM是否重複?
                foreach (ds_PSM pObj in pepObj.PsmList)
                {
                    if ((pObj.scanNumber == psmObj.scanNumber) && (pObj.rawDataFileName == psmObj.rawDataFileName))
                        is_PSMObjExist = true;
                }
                if (!is_PSMObjExist)
                    pepObj.PsmList.Add(psmObj);
                temp_protObj.Peptide_Dic.Add(pepObj_Index, pepObj);
            }
            else
            {   //case3. no repeat in Prot and Pep (No alternative Protein) 
                bool is_PSMObjExist = false;  //check PSM是否重複?
                foreach (ds_PSM pObj in pepObj.PsmList)
                {
                    if ((pObj.scanNumber == psmObj.scanNumber) && (pObj.rawDataFileName == psmObj.rawDataFileName))
                        is_PSMObjExist = true;
                }
                if (!is_PSMObjExist)
                    pepObj.PsmList.Add(psmObj);

                //(Check for : alternative Protein unit may have the same peptide)
                if (!protObj.Peptide_Dic.ContainsKey(pepObj_Index))
                    protObj.Peptide_Dic.Add(pepObj_Index, pepObj);

                this.searchResultObj.Protein_Dic.Add(protObj.ProtID, protObj);
            }
        }

        /// <summary>
        /// read protein Probability Table
        /// </summary>
        private void ProtXML_getProbTableInfo(XmlReader ProbTableReader)
        {
            ds_ProbabilityTable ProbTableObj = new ds_ProbabilityTable(); //no charge classfication
            
            while (ProbTableReader.Read())
            {
                if (ProbTableReader.NodeType != XmlNodeType.Element)
                    continue;

                if(ProbTableReader.Name == "protein_summary_data_filter") //record prob/FDR pair
                {
                    ds_ProbToFDR ProbToFDRObj = new ds_ProbToFDR();
                    ProbToFDRObj.Min_prob = (ProbTableReader.GetAttribute("min_probability") != null) ? float.Parse(ProbTableReader.GetAttribute("min_probability")) : 0;
                    ProbToFDRObj.FDR_rate = (ProbTableReader.GetAttribute("false_positive_error_rate") != null) ? float.Parse(ProbTableReader.GetAttribute("false_positive_error_rate")) : 0;
                    ProbTableObj.ProbToFDRlist.Add(ProbToFDRObj);
                }
            }
            this.searchResultObj.ProtProphetProb_Dic.Add("all", ProbTableObj); //no charge classfication, deafult = 0;
            ProbTableReader.Close();
        }

        private void ProtXML_getProtInfo(XmlReader ProtInfoReader)
        {
            List<string> protNameListInGroup = new List<string>();
            int group_number = (ProtInfoReader.GetAttribute("group_number") != null) ? int.Parse(ProtInfoReader.GetAttribute("group_number")) : 0;
            float group_prob = (ProtInfoReader.GetAttribute("probability") != null) ? float.Parse(ProtInfoReader.GetAttribute("probability")) : 0;
            
            while(ProtInfoReader.Read())
            {
                if (ProtInfoReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (ProtInfoReader.Name)
                {
                    case "protein":  //read protein name and info
                        string protName = (ProtInfoReader.GetAttribute("protein_name") != null) ? (ProtInfoReader.GetAttribute("protein_name")) : "";
                        protNameListInGroup.Add(protName);

                        if (this.searchResultObj.Protein_Dic.ContainsKey(protName))
                        {
                            this.searchResultObj.Protein_Dic[protName].Score = (ProtInfoReader.GetAttribute("probability") != null) ? double.Parse(ProtInfoReader.GetAttribute("probability")) : -1;
                        }
                        else
                        {   //exceptional case: protein in protein prophet but not in peptide prophet
                            StackTrace st = new StackTrace(new StackFrame(true));
                            Console.WriteLine(" [Exception] Stack trace for current level: {0}", st.ToString());
                            StackFrame sf = st.GetFrame(0);
                            Console.WriteLine(" File: {0}", sf.GetFileName());
                            Console.WriteLine(" Method: {0}", sf.GetMethod().Name);
                            Console.WriteLine(" Line Number: {0}", sf.GetFileLineNumber());
                            Console.WriteLine(" Column Number: {0}", sf.GetFileColumnNumber());
                        }
                        break;

                    default:
                        break;
                }
            }//end each "protein_group" Node
            this.searchResultObj.ProtGroupName_Dic.Add(group_number, protNameListInGroup);
            this.searchResultObj.ProtGroupProb_Dic.Add(group_number, group_prob);
        }

        /// <summary>
        /// Produce IASL Lab's modified Pep seq. (avoid different names of the same peptide)  
        /// </summary>
        /// <param name="pepObj"></param>
        /// <returns></returns>
        private string TranslateModPosToModSeq(ds_Peptide pepObj)
        {
            string returnseq = "";

            string sequence = pepObj.Sequence;
            List<ds_ModPosInfo> ModInfos = pepObj.ModPosList;
            Dictionary<int, int> ModInfoDic = new Dictionary<int, int>();

            foreach (ds_ModPosInfo ModInfo in ModInfos)
            {
                //--- 若兩個modification同時發生在一個aa上，就把mod mass加總 ---//
                if (ModInfoDic.ContainsKey(ModInfo.ModPos - 1))
                    ModInfoDic[ModInfo.ModPos - 1] = (int) (ModInfoDic[ModInfo.ModPos - 1] + ModInfo.ModMass);
                else
                    ModInfoDic.Add(ModInfo.ModPos - 1, (int)ModInfo.ModMass);
            }

            if (ModInfoDic.ContainsKey(-1)) //n-term first
            {
                returnseq+= "n["+ ModInfoDic[-1].ToString() +"]";
            }

            for (int i = 0; i < sequence.Length; i++)
            {
                returnseq += sequence[i];
                
                if (ModInfoDic.ContainsKey(i))
                    returnseq += "[" + ModInfoDic[i].ToString() + "]";
            }
            return returnseq;
        }

        private void GetLibraChannelMz(XmlReader libraSummaryReader)
        {
            Dictionary<int, double> chanMzDi = new Dictionary<int, double>();
            int channelNum;
            double mz;
            while (libraSummaryReader.Read())
            {
                if (libraSummaryReader.NodeType != XmlNodeType.Element)
                    continue;

                if (libraSummaryReader.Name == "fragment_masses") //
                {
                    channelNum = (libraSummaryReader.GetAttribute("channel") != null) ? int.Parse(libraSummaryReader.GetAttribute("channel")) : -1;
                    mz = (libraSummaryReader.GetAttribute("mz") != null) ? double.Parse(libraSummaryReader.GetAttribute("mz")) : -1.0;
                    if (!chanMzDi.ContainsKey(channelNum))
                        chanMzDi.Add(channelNum, mz);
                }
            }

            this.searchResultObj.LibraChannelMz_Dic = chanMzDi;
        }
    }
}

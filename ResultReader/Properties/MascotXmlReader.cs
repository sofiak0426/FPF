using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;

namespace ResultReader
{
    public class MascotXmlReader
    {
        ds_SearchResult searchResultObj = new ds_SearchResult();

        /// <summary>
        /// Parse_Mascot: one fileName you want to Parse plz put in first Param
        /// </summary>
        /// <param name="MascotFileName">Result file's Name(XML file) after running Mascot</param>
        /// <returns></returns>
        public ds_SearchResult ReadFile(string MascotFileName)
        {
            //read files
            XmlReader mascotXmlReader = XmlReader.Create(MascotFileName);
            ds_BasicInfo basicInfoObj = new ds_BasicInfo();
            this.searchResultObj.Source = SearchResult_Source.Mascot;

            while (mascotXmlReader.Read())
            {
                if (mascotXmlReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (mascotXmlReader.Name)
                {
                    case "header":
                    case "search_parameters":
                    case "format_parameters":
                        XmlReader basicInfoInnerReaderLv1 = mascotXmlReader.ReadSubtree(); //limit range to its subtree
                        basicInfoInnerReaderLv1.MoveToContent();
                        this.MascotXML_getBasicInfo(basicInfoInnerReaderLv1, basicInfoObj);
                        //---get basic information---// 
                        basicInfoInnerReaderLv1.Close();
                        break;

                    case "fixed_mods":
                    case "variable_mods":
                        XmlReader modInfoInnerReaderLv1 = mascotXmlReader.ReadSubtree(); //limit range to its subtree
                        modInfoInnerReaderLv1.MoveToContent();
                        // ---get modification information---//
                        this.MascotXML_getModInfo(modInfoInnerReaderLv1);
                        modInfoInnerReaderLv1.Close();
                        break;

                    case "hit":
                        XmlReader protPepInfoInnerReaderLv1 = mascotXmlReader.ReadSubtree();
                        protPepInfoInnerReaderLv1.MoveToContent();
                        // ---get psm, protein and peptide information---//
                        this.MascotXML_getProtPepPsmInfo(protPepInfoInnerReaderLv1);
                        protPepInfoInnerReaderLv1.Close();
                        break;

                    default:
                        break;
                }

            }
            this.searchResultObj.BasicInfoList.Add(basicInfoObj); //save basic info after read prot/pep hits
            mascotXmlReader.Close(); //close reader

            this.searchResultObj.RefreshPepProt_Dic();

            return this.searchResultObj;
        }

        /// <summary>
        /// get Basic Information from Mascot XML
        /// </summary>
        private void MascotXML_getBasicInfo(XmlReader BasicInfoReader, ds_BasicInfo BasicInfoObj)
        {

            while (BasicInfoReader.Read()) //search every Nodes "header"
            {
                if (BasicInfoReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (BasicInfoReader.Name)
                {
                    case "FILENAME":
                        if (BasicInfoReader.IsEmptyElement != true)
                        {
                            string[] FileInfos = BasicInfoReader.ReadElementContentAsString().Split(';');
                            string[] separators = { "File Name:" };
                            string[] FilePathStrs = (FileInfos[0].Split(separators, StringSplitOptions.RemoveEmptyEntries));
                            BasicInfoObj.filePath = FilePathStrs[0];
                        }
                        else
                            BasicInfoObj.filePath = "";
                        break;

                    case "MascotVer":
                        BasicInfoObj.Version = (BasicInfoReader.IsEmptyElement != true) ? BasicInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "URI":
                        BasicInfoObj.datName = (BasicInfoReader.IsEmptyElement != true) ? BasicInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "DB":
                        BasicInfoObj.DB = (BasicInfoReader.IsEmptyElement != true) ? BasicInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "FastaVer":
                        BasicInfoObj.FastaVer = (BasicInfoReader.IsEmptyElement != true) ? BasicInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "IT_MODS":
                        BasicInfoObj.searchPTM = (BasicInfoReader.IsEmptyElement != true) ? BasicInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "TOL":  //Peptide mass tolerance
                    case "TOLU":
                        BasicInfoObj.msTol += (BasicInfoReader.IsEmptyElement != true) ? BasicInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "ITOL":  //Fragment mass tolerancee
                    case "ITOLU":
                        BasicInfoObj.msmsTol += (BasicInfoReader.IsEmptyElement != true) ? BasicInfoReader.ReadElementContentAsString() : "";
                        break;
                    
                    case "INSTRUMENT":
                        BasicInfoObj.Instrument = (BasicInfoReader.IsEmptyElement != true) ? BasicInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "sigthreshold":
                        BasicInfoObj.pvalueThread = (BasicInfoReader.IsEmptyElement != true) ? BasicInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "TAXONOMY":
                        BasicInfoObj.taxonomy = (BasicInfoReader.IsEmptyElement != true) ? Path.GetFileName(BasicInfoReader.ReadElementContentAsString()).Replace(".", "").TrimStart(' ') : ""; 
                        break;

                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// get Modification Information from Mascot XML
        /// </summary>
        private void MascotXML_getModInfo(XmlReader ModInfoReader)
        {
            if (ModInfoReader.Name == "variable_mods")
                this.Read_variable_modification(ModInfoReader);
            else
                this.Read_fixed_modification(ModInfoReader);
        }

        private void Read_variable_modification(XmlReader ModInfoReader)
        {
            while (ModInfoReader.Read()) //search every Nodes in "variable_mods"
            {
                if (ModInfoReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (ModInfoReader.Name)
                {
                    case "modification": //each modification has one modpackObj
                        ModificationPack modpackObj = new ModificationPack();
                        List<NeutralLossPack> neupackList = new List<NeutralLossPack>();

                        modpackObj.Identifier = (ModInfoReader.GetAttribute("identifier") != null) ? int.Parse(ModInfoReader.GetAttribute("identifier")) : 0;
                        
                        XmlReader ModInfoReaderLv1 = ModInfoReader.ReadSubtree(); //limit range to its subtree
                        ModInfoReaderLv1.MoveToContent();
                        
                        while (ModInfoReaderLv1.Read()) //read each nodes in a modification Tree
                        {
                            if (ModInfoReaderLv1.NodeType != XmlNodeType.Element)
                                continue;

                            switch (ModInfoReaderLv1.Name)
                            {
                                case "name":
                                    modpackObj.ModCode = (ModInfoReaderLv1.IsEmptyElement != true) ? ModInfoReaderLv1.ReadElementContentAsString() : "";
                                    break;

                                case "delta":
                                    modpackObj.Mass_diff = (ModInfoReaderLv1.IsEmptyElement != true) ? ModInfoReaderLv1.ReadElementContentAsDouble() : 0.0;
                                    break;

                                case "neutral_loss":
                                    if(ModInfoReaderLv1.IsEmptyElement != true && ModInfoReader.GetAttribute("identifier") != null)
                                        neupackList.Add(new NeutralLossPack() { NeuIdentifier = int.Parse(ModInfoReader.GetAttribute("identifier")), Neutral_loss = ModInfoReaderLv1.ReadElementContentAsDouble() });
                                    break;

                                default:
                                    break;
                            }  
                        }

                        modpackObj.NeupackList = neupackList;
                        if (!this.searchResultObj.VarMod_Dic.ContainsKey(modpackObj.ModCode)) //if ModCode repeat don't save because its Mass_diff would be the same
                            this.searchResultObj.VarMod_Dic.Add(modpackObj.ModCode, modpackObj);
                        break;

                    default:
                        break;
                }
            }
        }

        private void Read_fixed_modification(XmlReader ModInfoReader)
        {
            while (ModInfoReader.Read()) //search every Nodes in "fixed_mods"
            {
                if (ModInfoReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (ModInfoReader.Name)
                {
                    case "modification": //each modification has one modpackObj
                        ModificationPack modpackObj = new ModificationPack();
                        List<NeutralLossPack> neupackList = new List<NeutralLossPack>();

                        modpackObj.Identifier = (ModInfoReader.GetAttribute("identifier") != null) ? int.Parse(ModInfoReader.GetAttribute("identifier")) : 0;

                        XmlReader ModInfoReaderLv1 = ModInfoReader.ReadSubtree(); //limit range to its subtree
                        ModInfoReaderLv1.MoveToContent();

                        while (ModInfoReaderLv1.Read()) //read each nodes in a modification Tree
                        {
                            if (ModInfoReaderLv1.NodeType != XmlNodeType.Element)
                                continue;

                            switch (ModInfoReaderLv1.Name)
                            {
                                case "name":
                                    modpackObj.ModCode = (ModInfoReaderLv1.IsEmptyElement != true) ? ModInfoReaderLv1.ReadElementContentAsString() : "";
                                    break;

                                case "delta":
                                    modpackObj.Mass_diff = (ModInfoReaderLv1.IsEmptyElement != true) ? ModInfoReaderLv1.ReadElementContentAsDouble() : 0.0;
                                    break;

                                case "neutral_loss":
                                    if (ModInfoReaderLv1.IsEmptyElement != true && ModInfoReader.GetAttribute("identifier") != null)
                                        neupackList.Add(new NeutralLossPack() { NeuIdentifier = int.Parse(ModInfoReader.GetAttribute("identifier")), Neutral_loss = ModInfoReaderLv1.ReadElementContentAsDouble() });
                                    break;

                                default:
                                    break;
                            }
                        }

                        modpackObj.NeupackList = neupackList;
                        if (!this.searchResultObj.FixedMod_Dic.ContainsKey(modpackObj.ModCode))
                            this.searchResultObj.FixedMod_Dic.Add(modpackObj.ModCode, modpackObj);
                        break;

                    default:
                        break;
                }
            }
        }


        /// <summary>
        /// get Prot/Pep/PSM Information from Mascot XML
        /// </summary>
        private void MascotXML_getProtPepPsmInfo(XmlReader protPepInfoReader)
        {
            ds_Protein protObj = new ds_Protein();

            while (protPepInfoReader.Read()) //search every Nodes in "hit"
            {
                if (protPepInfoReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (protPepInfoReader.Name)
                {
                    case "protein":
                        protObj.ProtID = (protPepInfoReader.GetAttribute("accession") != null) ? (protPepInfoReader.GetAttribute("accession")) : "";
                        break;
                    case "prot_desc":
                        protObj.Description = (protPepInfoReader.IsEmptyElement != true) ? protPepInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "prot_score":
                        protObj.Score = (protPepInfoReader.IsEmptyElement != true) ? double.Parse(protPepInfoReader.ReadElementContentAsString()) : -1.0;
                        break;

                    case "prot_mass":
                        protObj.Mass = (protPepInfoReader.IsEmptyElement != true) ? double.Parse(protPepInfoReader.ReadElementContentAsString()) : 0.0;
                        break;

                    case "peptide":
                        XmlReader protPepInfoReaderLv1 = protPepInfoReader.ReadSubtree();
                        protPepInfoReaderLv1.MoveToContent();
                        // --- read peptide and PSM info ---//
                        this.Read_pepInfo(protPepInfoReaderLv1, protObj);
                        protPepInfoReaderLv1.Close();
                        break;

                    default:
                        break;
                }

            }
        }

        private void Read_pepInfo(XmlReader PepInfoReader, ds_Protein protObj)
        {
            ds_Peptide pepObj = new ds_Peptide();
            ds_PSM psmObj = new ds_PSM(this.searchResultObj.Source);

            if (PepInfoReader.GetAttribute("rank") == null)
                return;

            psmObj.Rank = Int32.Parse(PepInfoReader.GetAttribute("rank"));
            psmObj.QueryNumber = (PepInfoReader.GetAttribute("query") != null) ? (PepInfoReader.GetAttribute("query")) : "";
            int isboldNum = (PepInfoReader.GetAttribute("isbold") != null) ? int.Parse(PepInfoReader.GetAttribute("isbold")) : 0;
            psmObj.IsBold = (isboldNum == 0)? false : true;

            while (PepInfoReader.Read()) //search every Nodes in "peptide"
            {
                if (PepInfoReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (PepInfoReader.Name)
                {
                    case "pep_exp_z":
                        psmObj.Charge = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsInt() : 0;
                        break;

                    case "pep_calc_mr": //Calculated Mr; calculated relative molecular mass.
                        pepObj.Pep_calc_mr = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsDouble() : 0.0;
                        break;

                    case "pep_delta": //Mass error (calculated Mr - experimental Mr)
                        psmObj.MassError = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsDouble() : 0.0;
                        break;

                    case "pep_score":
                        psmObj.Score = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsDouble() : -1.0;
                        break;

                    case "pep_seq":
                        pepObj.Sequence = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "pep_exp_mz": //Experimental m/z
                        psmObj.Pep_exp_mz = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsDouble() : 0.0;
                        break;

                    case "pep_exp_mr"://Experimental Mr
                        psmObj.Pep_exp_mr = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsDouble() : 0.0;
                        break;

                    case "pep_var_mod_pos":   //Need to check again
                        string modposStr = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsString() : "";

                        if (modposStr != "")
                        {
                            //string processingSequen = pepID.Sequence;
                            Dictionary<int, string> identifierModCode_dict = new Dictionary<int, string>();
                            foreach (ModificationPack modpack in this.searchResultObj.VarMod_Dic.Values)
                                identifierModCode_dict.Add(modpack.Identifier, modpack.ModCode); //make dictionary for modseq transfer
                            pepObj.ModifiedSequence = TranslateModPosToModSeq(pepObj.Sequence, modposStr, identifierModCode_dict);
                        }
                        else
                        {
                            pepObj.ModifiedSequence = "";
                        }
                        break;

                    case "pep_miss":
                        psmObj.MissedCleavage = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsInt() : 0;
                        break;

                    case "pep_start": //1 based residue count of peptide start position
                        pepObj.Pep_start = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsInt() : 0;
                        break;

                    case "pep_end": //1 based residue count of peptide end position
                        pepObj.Pep_end = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsInt() : 0;
                        break;

                    case "pep_res_before":
                        pepObj.PrevAA = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsString() : "";
                        break;

                    case "pep_res_after":
                        pepObj.NextAA = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsString() : "";;
                        break;

                    case "pep_expect": //Expectation value corresponding to ions score
                        psmObj.ExpectValue = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsDouble() : 0.0;
                        break;

                    case "pep_scan_title":
                        psmObj.Peptide_Scan_Title = (PepInfoReader.IsEmptyElement != true) ? PepInfoReader.ReadElementContentAsString() : "";
                        TitleParser tpObj = new TitleParser();
                        ds_ScanTitleInfo stiObj = tpObj.parse(psmObj.Peptide_Scan_Title);
                        psmObj.scanNumber = stiObj.scanNum;
                        psmObj.rawDataFileName = stiObj.rawDataName;
                        psmObj.SPCE = stiObj.SPCE;
                        break;
                     
                    default:
                        break;
                }
            }

            AttachInfo_to_terminationDS(protObj, pepObj, psmObj); //end each peptide in one hit info
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
                if (!temp_pepObj.PsmList.Contains(psmObj))
                    temp_pepObj.PsmList.Add(psmObj);
            }
            else if (this.searchResultObj.Protein_Dic.ContainsKey(protObj.ProtID))
            {    //case2. only same protein exists
                temp_protObj = this.searchResultObj.Protein_Dic[protObj.ProtID];
                if (!pepObj.PsmList.Contains(psmObj))
                    pepObj.PsmList.Add(psmObj);
                temp_protObj.Peptide_Dic.Add(pepObj_Index, pepObj);
            }
            else
            {   //case3. no repeat in Prot and Pep (No alternative Protein) 
                if (!pepObj.PsmList.Contains(psmObj))
                    pepObj.PsmList.Add(psmObj);

                protObj.Peptide_Dic.Add(pepObj_Index, pepObj);
                this.searchResultObj.Protein_Dic.Add(protObj.ProtID, protObj);
            }
        }

        private string TranslateModPosToModSeq(string sequence, string modpos, Dictionary<int, string> identifierModCode_dict)
        {
            string returnseq = "";

            if (modpos[0].ToString() != "0")
            {

                returnseq += "[" + identifierModCode_dict[Convert.ToInt32(modpos[0].ToString())] + "]";
                //if (identifier == Convert.ToInt32(modpos[0].ToString()))
                //    returnseq += "[" + modCode + "]";
                //else
                //    returnseq += "[" + null + "]";
            }

            string modposseq = modpos.Split('.')[1];

            for (int i = 0; i < sequence.Length; i++)
            {
                returnseq += sequence[i];

                if (modposseq[i].ToString() != "0")
                {
                                     
                    returnseq += "[" + identifierModCode_dict[Convert.ToInt32(modposseq[i].ToString())] + "]";

                    //if (identifier == Convert.ToInt32(modposseq[i].ToString()))
                    //    returnseq += "[" + modCode + "]";
                    //else
                    //    returnseq += "[" + null + "]";
                }
            }

            if (modpos[modpos.Length - 1].ToString() != "0")
            {
                returnseq += "[" + identifierModCode_dict[Convert.ToInt32(modpos[modpos.Length - 1].ToString())] + "]";
                //if (identifier == Convert.ToInt32(modpos[modpos.Length - 1].ToString()))
                //    returnseq += "[" + modCode + "]";
                //else
                //    returnseq += "[" + null + "]";
            }

            return returnseq;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace ResultReader
{
    public class XTandemXmlReader
    {
        ds_SearchResult searchResultObj = new ds_SearchResult();
        ds_BasicInfo basicInfoObj = new ds_BasicInfo();

        public ds_SearchResult ReadFile(string xmlFile)
        {
            //reader for xml file
            this.searchResultObj.Source = SearchResult_Source.XTandem_tXml;
            XmlReader xTandemReader = XmlReader.Create(xmlFile);

            while (xTandemReader.Read())
            {
                if (xTandemReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (xTandemReader.Name)
                {
                    case "group": //unit each PSM
                        //type: model
                        if(xTandemReader.GetAttribute("type") == null)
                            break;

                        if(xTandemReader.GetAttribute("type") == "model")
                        {
                            XmlReader groupReaderLv1 = xTandemReader.ReadSubtree(); //limit range to its subtree
                            groupReaderLv1.MoveToContent();
                            this.Xtandem_getProtPepPsmInfo(groupReaderLv1);
                            // ---get psm, protein and peptide information---//
                            groupReaderLv1.Close();
                        }

                        else if (xTandemReader.GetAttribute("type") == "parameters")
                        {
                            if(xTandemReader.GetAttribute("label") == null)
                                break;
                            else if (xTandemReader.GetAttribute("label") == "input parameters")
                            {
                                XmlReader groupReaderLv1 = xTandemReader.ReadSubtree(); //limit range to its subtree
                                groupReaderLv1.MoveToContent();
                                this.Xtandem_getbasicInfo(groupReaderLv1);
                                // ---get basic/mod info---//
                                groupReaderLv1.Close();
                                this.searchResultObj.BasicInfoList.Add(this.basicInfoObj);
                            }
                        }
                        //type : support(parameters)
                    break;

                    default:
                    break;
                }
            }

            xTandemReader.Close();
            this.searchResultObj.RefreshPepProt_Dic();
            return this.searchResultObj;
        }

        //read pep and prot info in each PSM(group) 
        private void Xtandem_getProtPepPsmInfo(XmlReader groupReader)
        {
            List<ds_Protein> protObjs = new List<ds_Protein>();
            List<ds_Peptide> pepObjs = new List<ds_Peptide>();
            List<ds_PSM> psmObjs = new List<ds_PSM>();

            ds_PSM shared_psmObj = new ds_PSM(this.searchResultObj.Source);

            shared_psmObj.QueryNumber = (groupReader.GetAttribute("id") != null) ? groupReader.GetAttribute("id") : "";
            shared_psmObj.Pep_exp_mass = (groupReader.GetAttribute("mh") != "") ? double.Parse(groupReader.GetAttribute("mh")) - 1.007276 : 0.0;
            shared_psmObj.Charge = (groupReader.GetAttribute("z") != "") ? int.Parse(groupReader.GetAttribute("z")) : 0;
            shared_psmObj.Precursor_mz = (shared_psmObj.Charge != 0) ? (shared_psmObj.Pep_exp_mass + shared_psmObj.Charge * 1.007276) / shared_psmObj.Charge : 0.0;
            shared_psmObj.ElutionTime = (groupReader.GetAttribute("rt") != "") ? float.Parse(groupReader.GetAttribute("rt")) : 0F;
            shared_psmObj.ExpectValue = (groupReader.GetAttribute("expect") != "") ? double.Parse(groupReader.GetAttribute("expect")) : 0.0;

            ds_Protein lastProcessedProtobj = new ds_Protein();  //最後處理的protein

            while (groupReader.Read())
            {
                if (groupReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (groupReader.Name)
                {
                    case "protein":
                        ds_Protein protobj = new ds_Protein();
                        protobj.Description = (groupReader.GetAttribute("label") != null) ? groupReader.GetAttribute("label") : "";
                        if (protobj.Description == "")
                            break;
                        else //get protein Name
                        {
                            string[] DescriptionStrs = protobj.Description.Split(' ');
                            protobj.ProtID = DescriptionStrs[0];
                            lastProcessedProtobj = protobj;
                        }
                        break;

                    case "file":
                        if (this.basicInfoObj.FastaVer == "") //if no info, save it
                            this.basicInfoObj.FastaVer = (groupReader.GetAttribute("URL") != null) ? groupReader.GetAttribute("URL") : "";
                        break;

                    case "peptide":
                        ds_Peptide pepObj = new ds_Peptide();
                        ds_PSM psmObj = ds_PSM.DeepClone<ds_PSM>(shared_psmObj);
                        XmlReader pepAndPsmReaderLv1 = groupReader.ReadSubtree(); //limit range to its subtree
                        pepAndPsmReaderLv1.MoveToContent();
                        this.Xtandem_getPepAndPsmInfo(pepAndPsmReaderLv1, pepObj, psmObj);
                        // ---get psm, protein and peptide information---//
                        pepAndPsmReaderLv1.Close();
                        psmObjs.Add(psmObj);
                        pepObjs.Add(pepObj);
                        protObjs.Add(lastProcessedProtobj);
                        break;

                    case "group":  //about this PSM description (group in group)
                        if (groupReader.GetAttribute("type") != null
                             && groupReader.GetAttribute("label") != null)
                        {
                            if (groupReader.GetAttribute("type") == "support"
                                && groupReader.GetAttribute("label") == "fragment ion mass spectrum")
                            {
                                XmlReader psmReaderLv1 = groupReader.ReadSubtree(); //limit range to its subtree
                                psmReaderLv1.MoveToContent();
                                //update detail psm info
                                ds_PSM temp_psmObj = this.ReadPsmSupInfo(psmReaderLv1);
                                psmReaderLv1.Close();
                                //update info in psmObjs
                                for (int i = 0; i < psmObjs.Count; i++)
                                {
                                    psmObjs[i].rawDataFileName = temp_psmObj.rawDataFileName;
                                    psmObjs[i].scanNumber = temp_psmObj.scanNumber;
                                    psmObjs[i].SPCE= temp_psmObj.SPCE;
                                }
                            }
                        }
                        break;

                    default:
                        break;
                }
            }

            //attach protein/peptide/psm to searchResultObj
            for (int i = 0; i < protObjs.Count; i++ )
            {
                ds_Protein protAttachObj = protObjs[i];
                ds_Peptide pepAttachObj = pepObjs[i];
                ds_PSM psmAttachObj = psmObjs[i];
                this.AttachInfo_to_terminationDS(protAttachObj, pepAttachObj, psmAttachObj);
            }
        }


        private void Xtandem_getPepAndPsmInfo(XmlReader pepReader, ds_Peptide pepObj, ds_PSM psmObj)
        {
            while (pepReader.Read())
            {
                if (pepReader.NodeType != XmlNodeType.Element)
                    continue;

                switch (pepReader.Name)
                {

                    case "domain":
                        pepObj.Pep_start = (pepReader.GetAttribute("start") != "") ? int.Parse(pepReader.GetAttribute("start")) : 0;
                        pepObj.Pep_end = (pepReader.GetAttribute("end") != "") ? int.Parse(pepReader.GetAttribute("end")) : 0;
                        pepObj.PrevAA = (pepReader.GetAttribute("pre") != null) ? pepReader.GetAttribute("pre") : "";
                        pepObj.NextAA = (pepReader.GetAttribute("post") != null) ? pepReader.GetAttribute("post") : "";
                        pepObj.Sequence = (pepReader.GetAttribute("seq") != null) ? pepReader.GetAttribute("seq") : "";
                        psmObj.Score = (pepReader.GetAttribute("hyperscore") != "") ? double.Parse(pepReader.GetAttribute("hyperscore")) : -1.0;
                        psmObj.MassError = (pepReader.GetAttribute("delta") != "") ? double.Parse(pepReader.GetAttribute("delta")) : 0.0;
                        psmObj.MissedCleavage = (pepReader.GetAttribute("missed_cleavages") != "") ? int.Parse(pepReader.GetAttribute("missed_cleavages")) : 0;
                        pepObj.Theoretical_mass = (pepReader.GetAttribute("mh") != "") ? double.Parse(pepReader.GetAttribute("mh")) - 1.007276 : 0.0;
                        break;

                    case "aa":
                        ds_ModPosInfo modInfoObj = new ds_ModPosInfo();
                        modInfoObj.ModMass = (pepReader.GetAttribute("modified") != null) ? double.Parse(pepReader.GetAttribute("modified")) : 0.0;
                        int AbsModPos = (pepReader.GetAttribute("at") != "") ? int.Parse(pepReader.GetAttribute("at")) : 0;
                        modInfoObj.ModPos = AbsModPos - pepObj.Pep_start + 1; //in N length seq, seq[1:N] 
                        pepObj.ModPosList.Add(modInfoObj);
                        break;

                    default:
                        break;
                }
            }

            //end: obtain modified seq of peptide in this protein Node
            this.TranslateModPosToModSeq(pepObj);
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

                //(alternative Protein unit has the same peptide)
                if (!protObj.Peptide_Dic.ContainsKey(pepObj_Index))
                    protObj.Peptide_Dic.Add(pepObj_Index, pepObj);

                this.searchResultObj.Protein_Dic.Add(protObj.ProtID, protObj);
            }
        }

        private void TranslateModPosToModSeq(ds_Peptide pepObj)         // insert modification mass and its position into the sequence.
        {
            List<int> modPos = new List<int>();
            List<double> modMassDiff = new List<double>();
            string tempSeq = "";

            for (int i = 0; i < pepObj.ModPosList.Count; i++ )
            {
                modPos.Add(pepObj.ModPosList[i].ModPos);
                modMassDiff.Add(pepObj.ModPosList[i].ModMass);
            }

            for (int i = 0; i < pepObj.Sequence.Length; i++)
            {
                if (!modPos.Contains(i+1))  // modPosinSeq[1:N], but Array_data[0:N-1]
                    tempSeq += pepObj.Sequence[i];
                else
                    tempSeq += pepObj.Sequence[i] + "[" + Math.Round(modMassDiff[modPos.IndexOf(i+1)], MidpointRounding.AwayFromZero) + "]";
            }

            pepObj.ModifiedSequence = tempSeq;
        }

        private void Xtandem_getbasicInfo(XmlReader basicInfoReader)
        {
            while (basicInfoReader.Read())
            {
                if (basicInfoReader.NodeType != XmlNodeType.Element)
                    continue;

                if (basicInfoReader.Name == "note"
                    && basicInfoReader.GetAttribute("type") != null
                    && basicInfoReader.GetAttribute("label") != null)
                {
                    if (basicInfoReader.GetAttribute("type") == "input")
                    {
                        this.basicInfoObj.DB = "Xtandem"; //rough info

                        switch (basicInfoReader.GetAttribute("label"))
                        {
                            case "list path, taxonomy information":
                                this.basicInfoObj.taxonomy = (basicInfoReader.IsEmptyElement != true) ? basicInfoReader.ReadElementContentAsString() : "";
                                break;

                            case "output, maximum valid expectation value":
                                this.basicInfoObj.pvalueThread = (basicInfoReader.IsEmptyElement != true) ? basicInfoReader.ReadElementContentAsString() : "";
                                break;

                            case "spectrum, fragment monoisotopic mass error":
                            case "spectrum, fragment monoisotopic mass error units":
                                this.basicInfoObj.msmsTol += (basicInfoReader.IsEmptyElement != true) ? basicInfoReader.ReadElementContentAsString() : "";
                                break;

                            case "spectrum, parent monoisotopic mass error minus":
                            case "spectrum, parent monoisotopic mass error units":
                                this.basicInfoObj.msTol += (basicInfoReader.IsEmptyElement != true) ? basicInfoReader.ReadElementContentAsString() : "";
                                break;
                            
                            case "output, path":
                                this.basicInfoObj.filePath = (basicInfoReader.IsEmptyElement != true) ? basicInfoReader.ReadElementContentAsString() : "";
                                break;

                            case "residue, modification mass":
                                if (basicInfoReader.IsEmptyElement != true)
                                    Read_fixed_modification(basicInfoReader);
                                break;

                            case "residue, potential modification mass":
                                if (basicInfoReader.IsEmptyElement != true)
                                    Read_variable_modification(basicInfoReader);
                                break;

                            default:
                                break;
                        }
                    } //end type:input Attribute  
                }
            } //end basicIndo reading   
        }

        private void Read_fixed_modification(XmlReader modInfoReader)
        {
            string modInfoStr = modInfoReader.ReadElementContentAsString();
            if (modInfoStr != "")
            {
                string[] infoStrs = modInfoStr.Split('@'); //57.022@C
                ModificationPack modpackObj = new ModificationPack();
                if (infoStrs.Length == 2)
                {
                    modpackObj.Identifier = this.searchResultObj.FixedMod_Dic.Count;
                    modpackObj.ModCode = infoStrs[1];
                    modpackObj.Mass_diff = double.Parse(infoStrs[0]);
                    if (!this.searchResultObj.FixedMod_Dic.ContainsKey(modpackObj.ModCode))
                        this.searchResultObj.FixedMod_Dic.Add(modpackObj.ModCode, modpackObj);
                }
            }
        }

        private void Read_variable_modification(XmlReader modInfoReader)
        {
            string modInfoStr = modInfoReader.ReadElementContentAsString();
            if (modInfoStr != "")
            {
                string[] infoStrs = modInfoStr.Split('@'); //15.994915@M
                ModificationPack modpackObj = new ModificationPack();
                if (infoStrs.Length == 2)
                {
                    modpackObj.Identifier = this.searchResultObj.VarMod_Dic.Count;
                    modpackObj.ModCode = infoStrs[1];
                    modpackObj.Mass_diff = double.Parse(infoStrs[0]);
                    if (!this.searchResultObj.VarMod_Dic.ContainsKey(modpackObj.ModCode))
                    {
                        this.searchResultObj.VarMod_Dic.Add(modpackObj.ModCode, modpackObj);
                        this.basicInfoObj.searchPTM += (modpackObj.ModCode + ", "); 
                    }
                }
            }
        }

        private ds_PSM ReadPsmSupInfo(XmlReader psmInfoReader)
        {
            ds_PSM psmObj = new ds_PSM(this.searchResultObj.Source);

            while (psmInfoReader.Read())
            {
                if (psmInfoReader.NodeType != XmlNodeType.Element)
                    continue;

                if (psmInfoReader.Name == "note" && psmInfoReader.GetAttribute("label") != null)
                {
                    if (psmInfoReader.GetAttribute("label") == "Description")
                    {
                        string psmInfoStr = psmInfoReader.ReadElementContentAsString();
                        TitleParser tt = new TitleParser();
                        ds_ScanTitleInfo titleObj = tt.parse(psmInfoStr);
                        psmObj.rawDataFileName = titleObj.rawDataName;
                        psmObj.scanNumber = titleObj.scanNum;
                        psmObj.SPCE = titleObj.SPCE;
                    }
                }
            }

            return psmObj;
        }

    }
}

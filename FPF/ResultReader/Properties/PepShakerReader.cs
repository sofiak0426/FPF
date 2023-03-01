using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using NPOI; 
using NPOI.HSSF; 
using NPOI.HSSF.UserModel; 
using NPOI.SS.UserModel;

namespace ResultReader
{
    public class PepShakerReader
    {
        ds_SearchResult searchResultObj = new ds_SearchResult();
        Dictionary<string, int> psmItemName_Dic = new Dictionary<string, int>();
        Dictionary<string, int> pepItemName_Dic = new Dictionary<string, int>();
        Dictionary<string, int> protItemName_Dic = new Dictionary<string, int>();

        public ds_SearchResult ReadFiles(string psmLv_Xls, string pepLv_Xls, string protLv_Xls)
        {
            this.searchResultObj.Source = SearchResult_Source.PeptideShaker;
            if ((psmLv_Xls != "") && (pepLv_Xls != "") && (protLv_Xls != ""))
            {
                this.ReadprotXls(protLv_Xls);
                this.ReadPepXls(pepLv_Xls);
                this.ReadPsmXls(psmLv_Xls);
                this.searchResultObj.Filter_Peptide(0.0F);
                this.searchResultObj.PrintProtPepIdToFile();
                this.searchResultObj.RefreshPepProt_Dic();
                this.searchResultObj.RefreshPepProtGroup_Dic();
            }
            return this.searchResultObj;
        }

        //check DicItem exist? ==> protein/peptide exist? ==>read PSM
        private void ReadPsmXls(string psmLv_Xls)
        {
            HSSFWorkbook wk;
            HSSFSheet hst;
            HSSFRow hr;
            FileStream fs = new FileStream(psmLv_Xls, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            wk = new HSSFWorkbook(fs);   //file ptr to HSS_wk  //check excel open
            hst = (HSSFSheet)wk.GetSheetAt(0);
            hr = (HSSFRow)hst.GetRow(0); //get row info #0
            this.DicSaveItemName(hr, this.psmItemName_Dic);

            //=========== DicItem exist? ======================//
            if (this.PsmDicCheckItemExist(this.psmItemName_Dic) == true) 
            {
                for (int j = 1; j <= hst.LastRowNum; j++)
                {
                    hr = (HSSFRow)hst.GetRow(j);
                    bool readRowFlag = false;  //only read psm one time in one row.
                    ds_PSM PsmObj = new ds_PSM(this.searchResultObj.Source);

                    //read whether protein and pepModSeq in Dic?  If yes : read its PSM further.
                    string protStr = hr.GetCell(this.psmItemName_Dic["Protein(s)"]) != null ? hr.GetCell(this.psmItemName_Dic["Protein(s)"]).ToString() : "";
                    string pepSeqStr = hr.GetCell(this.psmItemName_Dic["Sequence"]) != null ? hr.GetCell(this.psmItemName_Dic["Sequence"]).ToString() : "";
                    string ModpepSeqStr = hr.GetCell(this.psmItemName_Dic["Modified Sequence"]) != null ? hr.GetCell(this.psmItemName_Dic["Modified Sequence"]).ToString() : "";
                    string VarModStr = hr.GetCell(this.psmItemName_Dic["Variable Modifications"]) != null ? hr.GetCell(this.psmItemName_Dic["Variable Modifications"]).ToString() : "";
                    string FixModStr = hr.GetCell(this.psmItemName_Dic["Fixed Modifications"]) != null ? hr.GetCell(this.psmItemName_Dic["Fixed Modifications"]).ToString() : "";
                    string[] protNames = protStr.Split(',');

                    //=========== protein/peptide exist? ======================//
                    for (int i = 0; i < protNames.GetLength(0); i++)
                    {
                        string ProtName = protNames[i].Trim(); //Protein name for PSM saving
                        string protDescrip = protNames[i].Trim();
                        bool existProtPepFlag = false;
                        string pepModSeqName = ""; //pepModSeq name for PSM saving
                        double pepTheoryMass = 0.0; //calculated Mr

                        //check Dic contains this protein
                        if (this.searchResultObj.Protein_Dic.ContainsKey(ProtName))
                        {
                            ds_Protein proteinObj = this.searchResultObj.Protein_Dic[ProtName];
                            ds_Peptide temp_peptideObj = new ds_Peptide();
                            this.StoreModPosList(temp_peptideObj, VarModStr, FixModStr);
                            this.GetPepModSeq(temp_peptideObj, pepSeqStr, ModpepSeqStr);
                            //change Sequence to ModSequence according to ModPosList information

                            //check peptide name
                            if (proteinObj.Peptide_Dic.ContainsKey(temp_peptideObj.ModifiedSequence))
                            {
                                pepModSeqName = temp_peptideObj.ModifiedSequence;
                                existProtPepFlag = true;
                            }
                        }

                        if (!existProtPepFlag)
                            continue; //next protein in "for loop" 
                        else //============= read PSM and add this PSM Node into List ================//
                        {
                            
                            if (readRowFlag == false)
                            {
                                int RankNum = hr.GetCell(this.psmItemName_Dic["Rank"]) != null ? Int32.Parse(hr.GetCell(this.psmItemName_Dic["Rank"]).ToString()) : 0;
                                int MissedCleavagesNum = hr.GetCell(this.psmItemName_Dic["Missed Cleavages"]) != null ? Int32.Parse(hr.GetCell(this.psmItemName_Dic["Missed Cleavages"]).ToString()) : 0;
                                string spectrumFileStr = hr.GetCell(this.psmItemName_Dic["Spectrum File"]) != null ? hr.GetCell(this.psmItemName_Dic["Spectrum File"]).ToString() : "";
                                string spectrumTitleStr = hr.GetCell(this.psmItemName_Dic["Spectrum Title"]) != null ? hr.GetCell(this.psmItemName_Dic["Spectrum Title"]).ToString() : "";
                                int spectrumScanNum = hr.GetCell(this.psmItemName_Dic["Spectrum Scan Number"]) != null ? Int32.Parse(hr.GetCell(this.psmItemName_Dic["Spectrum Scan Number"]).ToString()) : 0;
                                int RtNum = hr.GetCell(this.psmItemName_Dic["RT"]) != null ? Int32.Parse(hr.GetCell(this.psmItemName_Dic["RT"]).ToString()) : 0;
                                double massOverzNum = hr.GetCell(this.psmItemName_Dic["m/z"]) != null ? double.Parse(hr.GetCell(this.psmItemName_Dic["m/z"]).ToString()) : 0.0;
                                string zStr = hr.GetCell(this.psmItemName_Dic["Identification Charge"]) != null ? hr.GetCell(this.psmItemName_Dic["Identification Charge"]).ToString() : "";
                                pepTheoryMass = hr.GetCell(this.psmItemName_Dic["Theoretical Mass"]) != null ? double.Parse(hr.GetCell(this.psmItemName_Dic["Theoretical Mass"]).ToString()) : 0.0;
                                double massOverzErrorNum = hr.GetCell(this.psmItemName_Dic["Precursor m/z Error [Da]"]) != null ? double.Parse(hr.GetCell(this.psmItemName_Dic["Precursor m/z Error [Da]"]).ToString()) : 0.0;
                                double psmScore = hr.GetCell(this.psmItemName_Dic["Confidence [%]"]) != null ? double.Parse(hr.GetCell(this.psmItemName_Dic["Confidence [%]"]).ToString()) : -1.0;
                                string ValidationStr = hr.GetCell(this.psmItemName_Dic["Validation"]) != null ? hr.GetCell(this.psmItemName_Dic["Validation"]).ToString() : "";
                                string AlgorithmScoreNote = hr.GetCell(this.psmItemName_Dic["Algorithm Score"]) != null ? hr.GetCell(this.psmItemName_Dic["Algorithm Score"]).ToString() : "";

                                PsmObj.Peptide_Scan_Title = spectrumTitleStr;
                                PsmObj.QueryNumber = spectrumTitleStr;
                                TitleParser tt = new TitleParser();
                                ds_ScanTitleInfo titleObj = tt.parse(spectrumTitleStr);
                                PsmObj.rawDataFileName = titleObj.rawDataName;
                                PsmObj.scanNumber = titleObj.scanNum;
                                PsmObj.SPCE = titleObj.SPCE;
                                PsmObj.ElutionTime = RtNum;
                                PsmObj.Rank = RankNum;
                                PsmObj.Score = psmScore;
                                PsmObj.Validation = ValidationStr;

                                int zNum = 1;
                                if (zStr != "")
                                {
                                    string[] zStrs = zStr.Split('+');
                                    zNum = Int32.Parse(zStrs[0]);
                                }
                                PsmObj.Charge = zNum;
                                PsmObj.Pep_exp_mr = massOverzNum * Math.Abs(zNum) - Math.Abs(zNum);
                                PsmObj.Pep_exp_mz = PsmObj.Pep_exp_mr / Math.Abs(zNum);
                                PsmObj.MassError = massOverzErrorNum * Math.Abs(zNum);
                                PsmObj.MissedCleavage = MissedCleavagesNum;
                                PsmObj.AlgorithmScore = AlgorithmScoreNote;
                                readRowFlag = true;  //have read this PSM in this row
                            }

                            ds_Peptide peptideObj = this.searchResultObj.Protein_Dic[ProtName].Peptide_Dic[pepModSeqName];
                            peptideObj.Pep_calc_mr = pepTheoryMass;
                            peptideObj.PsmList.Add(PsmObj); //add PSM for this peptide of the specific protein.
                        }//end add psm node 

                    } //end each protein in each line.

                } //end reading each row 
            } //end Reading this PSM xls file.
            wk.Clear();
        }

        private void ReadPepXls(string pepLv_Xls)
        {
            HSSFWorkbook wk;
            HSSFSheet hst;
            HSSFRow hr;
            FileStream fs = new FileStream(pepLv_Xls, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            wk = new HSSFWorkbook(fs);   //file ptr to HSS_wk  //check excel open
            hst = (HSSFSheet)wk.GetSheetAt(0);
            hr = (HSSFRow)hst.GetRow(0); //get row info #0
            this.DicSaveItemName(hr, this.pepItemName_Dic);

            if (this.PepDicCheckItemExist(this.pepItemName_Dic) == true) //protein and peptide info
            {
                for (int rowNum = 1; rowNum <= hst.LastRowNum; rowNum++) //read row by row
                {
                    hr = (HSSFRow)hst.GetRow(rowNum);
                    //Parse protein/peptide info
                    string protStr = hr.GetCell(this.pepItemName_Dic["Protein(s)"]) != null ? hr.GetCell(this.pepItemName_Dic["Protein(s)"]).ToString() : "";
                    string protDescripStr = hr.GetCell(this.pepItemName_Dic["Description(s)"]) != null ? hr.GetCell(this.pepItemName_Dic["Description(s)"]).ToString() : "";
                    string pepSeqStr = hr.GetCell(this.pepItemName_Dic["Sequence"]) != null ? hr.GetCell(this.pepItemName_Dic["Sequence"]).ToString() : "";
                    string ModpepSeqStr = hr.GetCell(this.pepItemName_Dic["Modified Sequence"]) != null ? hr.GetCell(this.pepItemName_Dic["Modified Sequence"]).ToString() : "";
                    string AAsBeforeStr = hr.GetCell(this.pepItemName_Dic["AAs Before"]) != null ? hr.GetCell(this.pepItemName_Dic["AAs Before"]).ToString() : "";
                    string AAsAfterStr = hr.GetCell(this.pepItemName_Dic["AAs After"]) != null ? hr.GetCell(this.pepItemName_Dic["AAs After"]).ToString() : "";
                    string VarModStr = hr.GetCell(this.pepItemName_Dic["Variable Modifications"]) != null ? hr.GetCell(this.pepItemName_Dic["Variable Modifications"]).ToString() : "";
                    string FixModStr = hr.GetCell(this.pepItemName_Dic["Fixed Modifications"]) != null ? hr.GetCell(this.pepItemName_Dic["Fixed Modifications"]).ToString() : "";
                    double pepScore = hr.GetCell(this.pepItemName_Dic["Confidence [%]"]) != null ? double.Parse(hr.GetCell(this.pepItemName_Dic["Confidence [%]"]).ToString()) : -1.0;
                    string ValidStr = hr.GetCell(this.pepItemName_Dic["Validation"]) != null ? hr.GetCell(this.pepItemName_Dic["Validation"]).ToString() : "";
                    int validPsmNum = hr.GetCell(this.pepItemName_Dic["#Validated PSMs"]) != null ? Int32.Parse(hr.GetCell(this.pepItemName_Dic["#Validated PSMs"]).ToString()) : 0;

                    if (validPsmNum == 0)  //no need to read this peptide without any valid PSM
                        continue;

                    ds_Peptide peptideObj = new ds_Peptide();  //Save info into into temp ds_peptide

                    peptideObj.Sequence = pepSeqStr;
                    peptideObj.Score = pepScore;

                    peptideObj.Validation = ValidStr;

                    this.StoreModPosList(peptideObj, VarModStr, FixModStr);
                    this.GetPepModSeq(peptideObj, pepSeqStr, ModpepSeqStr);
                    //change Sequence to ModSequence according to ModPosList information
       
                    //save peptide info into existed proteinObj
                    string[] protNames = protStr.Split(';');
                    string[] protDescrips = protDescripStr.Split(';');
                    string[] AAsBefores = AAsBeforeStr.Split(';');
                    string[] AAsAfters = AAsAfterStr.Split(';');

                    for (int i = 0; i < protNames.GetLength(0); i++)
                    {
                        string ProtName = protNames[i].Trim();
                        string protDescrip = protNames[i].Trim();

                        if (AAsBefores.Length-1 >=i)
                            peptideObj.PrevAA = AAsBefores[i].Trim();

                        if (AAsAfters.Length-1 >=i)
                            peptideObj.NextAA = AAsAfters[i].Trim();

                        //only update Protein has appeared in protXls
                        if(this.searchResultObj.Protein_Dic.ContainsKey(ProtName))
                        {
                            ds_Protein proteinObj = this.searchResultObj.Protein_Dic[ProtName];
                            proteinObj.Description = protDescrip;
                            proteinObj.Peptide_Dic.Add(peptideObj.ModifiedSequence, peptideObj);
                        }

                    }  //end each protein in each row

                }  //end reading in each row

            }  //end reading peptide xls file.
            wk.Clear();
        }

        private void ReadprotXls(string protLv_Xls)
        {
            HSSFWorkbook wk;
            HSSFSheet hst;
            HSSFRow hr;
            FileStream fs = new FileStream(protLv_Xls, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            wk = new HSSFWorkbook(fs);   //file ptr to HSS_wk  //check excel open
            hst = (HSSFSheet)wk.GetSheetAt(0);
            hr = (HSSFRow)hst.GetRow(0); //get row info #0
            this.DicSaveItemName(hr, this.protItemName_Dic);

            //Parse protein group/protein info
            if (this.protItemName_Dic.ContainsKey("Protein Group")
                && this.protItemName_Dic.ContainsKey("Confidence [%]")
                && this.protItemName_Dic.ContainsKey("Validation")
                && this.protItemName_Dic.ContainsKey("#Validated Peptides")
                && this.protItemName_Dic.ContainsKey("#Validated PSMs"))
            {
                for (int rowNum = 1; rowNum <= hst.LastRowNum; rowNum++) //read row by row
                {
                    hr = (HSSFRow)hst.GetRow(rowNum);

                    string protGroupStr = hr.GetCell(this.protItemName_Dic["Protein Group"]) != null ? hr.GetCell(this.protItemName_Dic["Protein Group"]).ToString() : "";
                    double protGroupProb = hr.GetCell(this.protItemName_Dic["Confidence [%]"]) != null ? double.Parse(hr.GetCell(this.protItemName_Dic["Confidence [%]"]).ToString()) : -1.0F;
                    string protGroupValidstr = hr.GetCell(this.protItemName_Dic["Validation"]) != null ? hr.GetCell(this.protItemName_Dic["Validation"]).ToString() : "";
                    int validPepNum = hr.GetCell(this.protItemName_Dic["#Validated Peptides"]) != null ? Int32.Parse(hr.GetCell(this.protItemName_Dic["#Validated Peptides"]).ToString()) : 0;
                    int validPsmNum = hr.GetCell(this.protItemName_Dic["#Validated PSMs"]) != null ? Int32.Parse(hr.GetCell(this.protItemName_Dic["#Validated PSMs"]).ToString()) : 0;

                    if ((validPepNum == 0) || (validPsmNum == 0))
                        continue;    //no need to read this protein without any valid peptide/PSM
                    
                    string[] protNames = protGroupStr.Split(',');
                    List <string> protNameList =  new List<string>();

                    for (int j = 0; j < protNames.GetLength(0); j++) //each protein in one row
                    {
                        string tmpProtName = protNames[j].Trim();
                        ds_Protein ProteinNode = new ds_Protein();
                        protNameList.Add(tmpProtName);
                        ProteinNode.ProtID = tmpProtName;
                        
                        //proteinDic
                        if (!this.searchResultObj.Protein_Dic.ContainsKey(tmpProtName))
                            this.searchResultObj.Protein_Dic.Add(tmpProtName, ProteinNode);

                        //protein Validation
                        searchResultObj.Protein_Dic[tmpProtName].Validation = protGroupValidstr;

                    }

                    searchResultObj.ProtGroupProb_Dic.Add(rowNum, protGroupProb);
                    searchResultObj.ProtGroupName_Dic.Add(rowNum, protNameList);
                }   
            }

            wk.Clear();
        }

        private void DicSaveItemName(HSSFRow hr, Dictionary<string, int> ItemName_Dic)
        {
            int ColnumNum = hr.Cells.Count;

            for (int i = 1; i <= (ColnumNum - 1); i++) //column 0 is row number
            {
                string itemStr = hr.GetCell(i) != null ? hr.GetCell(i).ToString() : "" ;

                if (!ItemName_Dic.ContainsKey(itemStr))
                    ItemName_Dic.Add(itemStr, i);

                else if (itemStr == "Validation" || itemStr == "Starred" || itemStr == "Hidden")
                {   //additional Validation//Starred//Hidden
                    int SafeCount = 2;
                    while (SafeCount <= 100)  //ex: save as "Validation2", "Validation3"... 
                    {
                        string PostItemStr = itemStr + SafeCount.ToString();
                        if (!ItemName_Dic.ContainsKey(PostItemStr))
                        {
                            ItemName_Dic.Add(PostItemStr, i);
                            break;
                        }
                        SafeCount++;
                    }
                }
            }
        }

        private bool PsmDicCheckItemExist(Dictionary<string, int> ItemName_Dic)
        {
            bool test_flag = true;
            string[] checkItem = { "Rank", "Protein(s)", "Sequence", "Modified Sequence",
                                 "Missed Cleavages", "Spectrum File",
                                 "Spectrum Title", "Spectrum Scan Number", "RT",
                                 "Variable Modifications", "Fixed Modifications",
                                 "m/z", "Identification Charge", "Theoretical Mass",
                                 "Precursor m/z Error [Da]", "Algorithm Score",
                                 "Confidence [%]", "Validation"};

            for (int i = 0; i < checkItem.GetLength(0); i++)
            {
                if (!ItemName_Dic.ContainsKey(checkItem[i]))
                    test_flag = false;
            }

            return test_flag;
        }

        private bool PepDicCheckItemExist(Dictionary<string, int> ItemName_Dic)
        {
            bool test_flag = true;
            string[] checkItem = { "Protein(s)", "Description(s)", "Sequence", "Modified Sequence",
                                 "AAs Before", "AAs After", "Variable Modifications",
                                 "Fixed Modifications", "Confidence [%]", "Validation",
                                 "#Validated PSMs"};

            for (int i = 0; i < checkItem.GetLength(0); i++)
            {
                if (!ItemName_Dic.ContainsKey(checkItem[i]))
                    test_flag = false;
            }

            return test_flag;
        }

        //get ModPos information from VarModStr/FixModStr
        private void StoreModPosList(ds_Peptide pepObj, string VarModStr, string FixModStr)
        {
            //Parse Var Mod position
            string[] separators = {"),", ")"};
            if (VarModStr != "") //ex: Acetylation of protein N-term (1), Oxidation of M (6, 3),  Sxidation of M (7, 8)
            {
                string[] VarMods = VarModStr.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                        
                foreach(string VarMod in VarMods)
                {
                    if (VarMod.Substring(0, 3).Equals("Ace"))
                        continue; //skip n-term mod
                            
                    //ex: Oxidation of M (6, 3
                    string[] VarModInfo = VarMod.Split('(');
                    if (VarModInfo.Length != 2)  //Mod: Name(0) + Pos(1)
                        continue;

                    string modName = VarModInfo[0].Trim().Substring(VarModInfo[0].Trim().Length-1, 1); //Last Alpha M
                    string[] modPoses = VarModInfo[1].Split(',');
                    foreach (string modPos in modPoses) //get each Mod position
                    {
                        ds_ModPosInfo PosInfoObj = new ds_ModPosInfo();
                        PosInfoObj.ModPos = Int32.Parse(modPos.Trim());
                        PosInfoObj.ModMass = this.ModAlphaToMass(modName);
                        pepObj.ModPosList.Add(PosInfoObj);
                    }
                            
                }
            }

            //Parse Fix Mod position
            if (FixModStr != "")
            {
                string[] FixMods = FixModStr.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                foreach (string FixMod in FixMods)
                {
                    if (FixMod.Substring(0, 3).Equals("Ace"))
                        continue; //skip n-term mod

                    //ex: Oxidation of C (6, 3
                    string[] FixModInfo = FixMod.Split('(');
                    if (FixModInfo.Length != 2)  //Mod: Name(0) + Pos(1)
                        continue;

                    string modName = FixModInfo[0].Trim().Substring(FixModInfo[0].Trim().Length - 1, 1); //Last Alpha M
                    string[] modPoses = FixModInfo[1].Split(',');
                    foreach (string modPos in modPoses) //get each Mod position
                    {
                        ds_ModPosInfo PosInfoObj = new ds_ModPosInfo();
                        PosInfoObj.ModPos = Int32.Parse(modPos.Trim());
                        PosInfoObj.ModMass = this.ModAlphaToMass(modName);
                        pepObj.ModPosList.Add(PosInfoObj);
                    }
                }
            }
        }
            
        //change original PepSequence to ModSequence according to ModPosList information
        private void GetPepModSeq(ds_Peptide pepObj, string SeqStrinXls, string MosSeqStrinXls)
        {
            string pepModSeq = SeqStrinXls; //initial
            //pepModSeq will save in peptideObj.ModifiedSequence

            if (pepObj.ModPosList.Count > 0)  //trans OrgpepSeq by info in PepObj.ModPoslist
            {
                int[] mod_indexs = new int[pepObj.ModPosList.Count];
                double[] mod_massdiff = new double[pepObj.ModPosList.Count];

                for (int i = 0; i < pepObj.ModPosList.Count; i++)
                {
                    mod_indexs[i] = pepObj.ModPosList[i].ModPos;
                    mod_massdiff[i] = pepObj.ModPosList[i].ModMass;
                }
                Array.Sort(mod_indexs, mod_massdiff);

                for (int i = (pepObj.ModPosList.Count - 1); i >= 0; i--) //from right to left
                {
                    string InsertStr = ModMassdiffToAlphaStr(mod_massdiff[i]);
                    if (InsertStr != "")
                        pepModSeq = pepModSeq.Insert(mod_indexs[i], InsertStr);
                }
            }
            //change pepModSeq representation -prefix
            string[] pepModNames = MosSeqStrinXls.Split('-');
            string modSeq = "";
            if (pepModNames[0].Trim() == "ace")
                modSeq = "n[43]" + pepModSeq;
            else
                modSeq = pepModSeq;

            pepObj.ModifiedSequence = modSeq;
        }

        //need to trans alpha to Mass when transfering ModSeq
        private double ModAlphaToMass(string ModAlpha)
        {
            double Mass = 0.0;

            switch (ModAlpha)
            {
                case ("M"):
                    Mass = 147.0;
                    break;

                case ("C"):
                    Mass = 160.0;
                    break;

                default:
                    break;
            }

            return Mass;
        }


        //need to trans Mass to Alpha when transfering ModSeq
        private string ModMassdiffToAlphaStr(double Mass)
        {
            string AlphaStr = "";

            switch (Convert.ToInt32(Mass))
            {
                case (147):
                    AlphaStr = "[147]";
                    break;

                case (160):
                    AlphaStr = "";  //neglect C[12], just C
                    break;

                default:
                    AlphaStr = "[0]";
                    break;
            }

            return AlphaStr;
        }

    }
}

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
                if(Path.GetExtension(psmLv_Xls).Equals(".txt"))
                    this.ReadProtTXT(protLv_Xls);
                else
                    this.ReadprotXls(protLv_Xls);

                if (Path.GetExtension(pepLv_Xls).Equals(".txt"))
                    this.ReadPepTXT(pepLv_Xls);
                else
                    this.ReadPepXls(pepLv_Xls);

                if (Path.GetExtension(psmLv_Xls).Equals(".txt"))
                    this.ReadPsmTXT(psmLv_Xls);
                else
                    this.ReadPsmXls(psmLv_Xls);
                //this.searchResultObj.Filter_Peptide(0.0F);
                //this.searchResultObj.PrintProtPepIdToFile();
                this.searchResultObj.CheckSupportFromBottom();  //check完後也在函式中執行了  this.searchResultObj.RefreshPepProt_Dic(); this.searchResultObj.RefreshPepProtGroup_Dic();
                this.searchResultObj.ComparePepProtDiKeys();
                this.searchResultObj.DeterminePeptideUniqueness();
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
            string[] itemcheckarray = {"Protein(s)", "Sequence", "Modified Sequence", "Variable Modifications", "Fixed Modifications", 
                                       "Rank", "Missed Cleavages", "Spectrum File", "Spectrum Title", "Spectrum Scan Number", "RT", "m/z", "Identification Charge",
                                       "Theoretical Mass", "Precursor m/z Error [Da]", "Confidence [%]", "Validation", "Algorithm Score"};
            Dictionary<string, string> item_interest = new Dictionary<string, string>();

            foreach (string itemname in itemcheckarray)
            {
                if (this.psmItemName_Dic.ContainsKey(itemname))
                {
                    item_interest.Add(itemname, "");
                }
                else
                {
                    item_interest.Add(itemname, "");
                    Console.WriteLine("No {0} item exists.", itemname);
                }
            }

            //=========== DicItem exist? ======================//
            for (int j = 1; j <= hst.LastRowNum; j++)
            {
                hr = (HSSFRow)hst.GetRow(j);
                bool readRowFlag = false;  //only read psm one time in one row.
                ds_PSM PsmObj = new ds_PSM(this.searchResultObj.Source);

                item_interest["Protein(s)"] = this.psmItemName_Dic.ContainsKey("Protein(s)") == true ? hr.GetCell(this.psmItemName_Dic["Protein(s)"]).ToString() : "";
                item_interest["Sequence"] = this.psmItemName_Dic.ContainsKey("Sequence") == true ? hr.GetCell(this.psmItemName_Dic["Sequence"]).ToString() : "";
                item_interest["Modified Sequence"] = this.psmItemName_Dic.ContainsKey("Modified Sequence") == true ? hr.GetCell(this.psmItemName_Dic["Modified Sequence"]).ToString() : "";
                item_interest["Variable Modifications"] = this.psmItemName_Dic.ContainsKey("Variable Modifications") == true ? hr.GetCell(this.psmItemName_Dic["Variable Modifications"]).ToString() : "";
                item_interest["Fixed Modifications"] = this.psmItemName_Dic.ContainsKey("Fixed Modifications") == true ? hr.GetCell(this.psmItemName_Dic["Fixed Modifications"]).ToString() : "";
                string temp_protNames = item_interest["Protein(s)"].Replace(", ", ";");  // 12/12 先用置換 把 ", " 換成 ";" ，因為檔案中有可能會有其他逗號使用，惟有接著空格的逗號: ", " 才能確定是分隔不同protNames
                string[] protNames = temp_protNames.Split(';');      // 12/12 原本是',' 但因為上一行換成";" 所以相應改變這邊用來分割的字符

                int count_sameProt = 0;
                if (protNames.GetLength(0) > 1)
                {
                    count_sameProt++;
                }

                //=========== protein/peptide exist? ======================//
                for (int i = 0; i < protNames.GetLength(0); i++)
                {
                    string ProtName = protNames[i].Trim(); //Protein name for PSM saving    *12/12經過上面的修改，這邊就應該不是一定要 Trim()
                    string protDescrip = protNames[i].Trim();                             // 12/12同上
                    bool existProtPepFlag = false;
                    string pepModSeqName = ""; //pepModSeq name for PSM saving
                    double pepTheoryMass = 0.0; //calculated Mr

                    //check Dic contains this protein
                    if (this.searchResultObj.Protein_Dic.ContainsKey(ProtName))
                    {
                        ds_Protein proteinObj = this.searchResultObj.Protein_Dic[ProtName];
                        ds_Peptide temp_peptideObj = new ds_Peptide();
                        this.StoreModPosList(temp_peptideObj, item_interest["Variable Modifications"], item_interest["Fixed Modifications"]);
                        temp_peptideObj.ModifiedSequence = item_interest["Modified Sequence"];
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
                            item_interest["Rank"] = this.psmItemName_Dic.ContainsKey("Rank") == true ? hr.GetCell(this.psmItemName_Dic["Rank"]).ToString() : "0";
                            item_interest["Missed Cleavages"] = this.psmItemName_Dic.ContainsKey("Missed Cleavages") == true ? hr.GetCell(this.psmItemName_Dic["Missed Cleavages"]).ToString() : "0";
                            item_interest["Spectrum File"] = this.psmItemName_Dic.ContainsKey("Spectrum File") == true ? hr.GetCell(this.psmItemName_Dic["Spectrum File"]).ToString() : "";
                            item_interest["Spectrum Title"] = this.psmItemName_Dic.ContainsKey("Spectrum Title") == true ? hr.GetCell(this.psmItemName_Dic["Spectrum Title"]).ToString() : "";
                            item_interest["Spectrum Scan Number"] = this.psmItemName_Dic.ContainsKey("Spectrum Scan Number") == true ? hr.GetCell(this.psmItemName_Dic["Spectrum Scan Number"]).ToString() : "0";
                            item_interest["RT"] = this.psmItemName_Dic.ContainsKey("RT") == true ? hr.GetCell(this.psmItemName_Dic["RT"]).ToString() : "0";
                            item_interest["m/z"] = this.psmItemName_Dic.ContainsKey("m/z") == true ? hr.GetCell(this.psmItemName_Dic["m/z"]).ToString() : "0.0";
                            item_interest["Identification Charge"] = this.psmItemName_Dic.ContainsKey("Identification Charge") == true ? hr.GetCell(this.psmItemName_Dic["Identification Charge"]).ToString() : "";
                            item_interest["Theoretical Mass"] = this.psmItemName_Dic.ContainsKey("Theoretical Mass") == true ? hr.GetCell(this.psmItemName_Dic["Theoretical Mass"]).ToString() : "0";
                            item_interest["Precursor m/z Error [Da]"] = this.psmItemName_Dic.ContainsKey("Precursor m/z Error [Da]") == true ? hr.GetCell(this.psmItemName_Dic["Precursor m/z Error [Da]"]).ToString() : "0";
                            item_interest["Confidence [%]"] = this.psmItemName_Dic.ContainsKey("Confidence [%]") == true ? hr.GetCell(this.psmItemName_Dic["Confidence [%]"]).ToString() : "-1.0";
                            item_interest["Validation"] = this.psmItemName_Dic.ContainsKey("Validation") == true ? hr.GetCell(this.psmItemName_Dic["Validation"]).ToString() : "";
                            item_interest["Algorithm Score"] = this.psmItemName_Dic.ContainsKey("Algorithm Score") == true ? hr.GetCell(this.psmItemName_Dic["Algorithm Score"]).ToString() : "";


                            PsmObj.Peptide_Scan_Title = item_interest["Spectrum Title"];
                            PsmObj.QueryNumber = PsmObj.Peptide_Scan_Title;
                            TitleParser tt = new TitleParser();
                            ds_ScanTitleInfo titleObj = tt.parse(PsmObj.Peptide_Scan_Title);
                            PsmObj.rawDataFileName = titleObj.rawDataName;
                            PsmObj.scanNumber = titleObj.scanNum;
                            PsmObj.SPCE = titleObj.SPCE;
                            PsmObj.ElutionTime = float.Parse(item_interest["RT"]);
                            PsmObj.Rank = Int32.Parse(item_interest["Rank"]);
                            PsmObj.Score = double.Parse(item_interest["Confidence [%]"]);
                            PsmObj.Validation = item_interest["Validation"];

                            int zNum = 1;
                            if (item_interest["Identification Charge"] != "")
                            {
                                string[] zStrs = item_interest["Identification Charge"].Split('+');
                                zNum = Int32.Parse(zStrs[0]);
                            }
                            PsmObj.Charge = zNum;
                            double massOverzNum = double.Parse(item_interest["m/z"]);
                            PsmObj.Pep_exp_mass = massOverzNum * Math.Abs(zNum) - Math.Abs(zNum);
                            PsmObj.Precursor_mz = massOverzNum;
                            double massOverzErrorNum = double.Parse(item_interest["Precursor m/z Error [Da]"]);
                            PsmObj.MassError = massOverzErrorNum * Math.Abs(zNum);
                            PsmObj.MissedCleavage = Int32.Parse(item_interest["Missed Cleavages"]);
                            PsmObj.AlgorithmScore = item_interest["Algorithm Score"];
                            readRowFlag = true;  //have read this PSM in this row    
                            pepTheoryMass = double.Parse(item_interest["Theoretical Mass"]);
                        }

                        ds_Peptide peptideObj = this.searchResultObj.Protein_Dic[ProtName].Peptide_Dic[pepModSeqName];
                        peptideObj.Theoretical_mass = pepTheoryMass;
                        peptideObj.PsmList.Add(PsmObj); //add PSM for this peptide of the specific protein.
                    }//end add psm node 

                } //end each protein in each line.

            } 
                //end reading each row 
            //end Reading this PSM xls file.
            wk.Clear();
        }

        private void CheckLostItem()
        {

        }

        private void ReadPsmTXT(string psmLv_Txt)
        {
            StreamReader txtReader = new StreamReader(psmLv_Txt);
            int colNum = this.DicSaveItemName_TXT(txtReader.ReadLine(), this.psmItemName_Dic);
            string[] itemcheckarray = {"Protein(s)", "Sequence", "Modified Sequence", "Variable Modifications", "Fixed Modifications", 
                                       "Rank", "Missed Cleavages", "Spectrum File", "Spectrum Title", "Spectrum Scan Number", "RT", "m/z", "Identification Charge",
                                       "Theoretical Mass", "Precursor m/z Error [Da]", "Confidence [%]", "Validation", "Algorithm Score"};
            Dictionary<string, string> item_interest = new Dictionary<string, string>();

            foreach (string itemname in itemcheckarray)
            {
                if (this.psmItemName_Dic.ContainsKey(itemname))
                {
                    item_interest.Add(itemname, "");
                }
                else
                {
                    item_interest.Add(itemname, "");
                    Console.WriteLine("No {0} item exists.", itemname);
                }
            }
            string line = "";
            int rowNum = 1;
            while ((line = txtReader.ReadLine()) != null)
            {
                bool readRowFlag = false;  //only read psm one time in one row.
                ds_PSM PsmObj = new ds_PSM(this.searchResultObj.Source);
                string[] elements = line.Replace("\n", "").Replace("\r", "").Split('\t');
                if (elements.GetLength(0) != colNum)
                    continue;

                item_interest["Protein(s)"] = this.psmItemName_Dic.ContainsKey("Protein(s)") == true ? elements[this.psmItemName_Dic["Protein(s)"]] : "";
                item_interest["Sequence"] = this.psmItemName_Dic.ContainsKey("Sequence") == true ? elements[this.psmItemName_Dic["Sequence"]] : "";
                item_interest["Modified Sequence"] = this.psmItemName_Dic.ContainsKey("Modified Sequence") == true ? elements[this.psmItemName_Dic["Modified Sequence"]] : "";
                item_interest["Variable Modifications"] = this.psmItemName_Dic.ContainsKey("Variable Modifications") == true ? elements[this.psmItemName_Dic["Variable Modifications"]] : "";
                item_interest["Fixed Modifications"] = this.psmItemName_Dic.ContainsKey("Fixed Modifications") == true ? elements[this.psmItemName_Dic["Fixed Modifications"]] : "";
                string temp_protNames = item_interest["Protein(s)"].Replace(", ", ";");  // 12/12 先用置換 把 ", " 換成 ";" ，因為檔案中有可能會有其他逗號使用，惟有接著空格的逗號: ", " 才能確定是分隔不同protNames
                string[] protNames = temp_protNames.Split(';');      // 12/12 原本是',' 但因為上一行換成";" 所以相應改變這邊用來分割的字符

                //=========== protein/peptide exist? ======================//
                for (int i = 0; i < protNames.GetLength(0); i++)
                {
                    string ProtName = protNames[i].Trim(); //Protein name for PSM saving    *12/12經過上面的修改，這邊就應該不是一定要 Trim()
                    string protDescrip = protNames[i].Trim();                             // 12/12同上
                    bool existProtPepFlag = false;
                    string pepModSeqName = ""; //pepModSeq name for PSM saving
                    double pepTheoryMass = 0.0; //calculated Mr

                    //check Dic contains this protein
                    if (this.searchResultObj.Protein_Dic.ContainsKey(ProtName))
                    {
                        ds_Protein proteinObj = this.searchResultObj.Protein_Dic[ProtName];
                        ds_Peptide temp_peptideObj = new ds_Peptide();
                        this.StoreModPosList(temp_peptideObj, item_interest["Variable Modifications"], item_interest["Fixed Modifications"]);
                        temp_peptideObj.ModifiedSequence = item_interest["Modified Sequence"];
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
                            item_interest["Rank"] = this.psmItemName_Dic.ContainsKey("Rank") == true ? elements[this.psmItemName_Dic["Rank"]] : "0";
                            item_interest["Missed Cleavages"] = this.psmItemName_Dic.ContainsKey("Missed Cleavages") == true ? elements[this.psmItemName_Dic["Missed Cleavages"]] : "0";
                            item_interest["Spectrum File"] = this.psmItemName_Dic.ContainsKey("Spectrum File") == true ? elements[this.psmItemName_Dic["Spectrum File"]] : "";
                            item_interest["Spectrum Title"] = this.psmItemName_Dic.ContainsKey("Spectrum Title") == true ? elements[this.psmItemName_Dic["Spectrum Title"]] : "";
                            item_interest["Spectrum Scan Number"] = this.psmItemName_Dic.ContainsKey("Spectrum Scan Number") == true ? elements[this.psmItemName_Dic["Spectrum Scan Number"]] : "0";
                            item_interest["RT"] = this.psmItemName_Dic.ContainsKey("RT") == true ? elements[this.psmItemName_Dic["RT"]] : "0";
                            item_interest["m/z"] = this.psmItemName_Dic.ContainsKey("m/z") == true ? elements[this.psmItemName_Dic["m/z"]] : "0.0";
                            item_interest["Identification Charge"] = this.psmItemName_Dic.ContainsKey("Identification Charge") == true ? elements[this.psmItemName_Dic["Identification Charge"]] : "";
                            item_interest["Theoretical Mass"] = this.psmItemName_Dic.ContainsKey("Theoretical Mass") == true ? elements[this.psmItemName_Dic["Theoretical Mass"]] : "0";
                            item_interest["Precursor m/z Error [Da]"] = this.psmItemName_Dic.ContainsKey("Precursor m/z Error [Da]") == true ? elements[this.psmItemName_Dic["Precursor m/z Error [Da]"]] : "0";
                            item_interest["Confidence [%]"] = this.psmItemName_Dic.ContainsKey("Confidence [%]") == true ? elements[this.psmItemName_Dic["Confidence [%]"]] : "-1.0";
                            item_interest["Validation"] = this.psmItemName_Dic.ContainsKey("Validation") == true ? elements[this.psmItemName_Dic["Validation"]] : "";
                            item_interest["Algorithm Score"] = this.psmItemName_Dic.ContainsKey("Algorithm Score") == true ? elements[this.psmItemName_Dic["Algorithm Score"]] : "";

                            PsmObj.Peptide_Scan_Title = item_interest["Spectrum Title"];
                            PsmObj.QueryNumber = PsmObj.Peptide_Scan_Title;
                            TitleParser tt = new TitleParser();
                            ds_ScanTitleInfo titleObj = tt.parse(PsmObj.Peptide_Scan_Title);
                            PsmObj.rawDataFileName = titleObj.rawDataName;
                            PsmObj.scanNumber = titleObj.scanNum;
                            PsmObj.SPCE = titleObj.SPCE;
                            PsmObj.ElutionTime = float.Parse(item_interest["RT"]);
                            PsmObj.Rank = Int32.Parse(item_interest["Rank"]);
                            PsmObj.Score = double.Parse(item_interest["Confidence [%]"]);
                            PsmObj.Validation = item_interest["Validation"];

                            int zNum = 1;
                            if (item_interest["Identification Charge"] != "")
                            {
                                string[] zStrs = item_interest["Identification Charge"].Split('+');
                                zNum = Int32.Parse(zStrs[0]);
                            }
                            PsmObj.Charge = zNum;
                            double massOverzNum = double.Parse(item_interest["m/z"]);
                            PsmObj.Pep_exp_mass = massOverzNum * Math.Abs(zNum) - Math.Abs(zNum);
                            PsmObj.Precursor_mz = massOverzNum;
                            double massOverzErrorNum = double.Parse(item_interest["Precursor m/z Error [Da]"]);
                            PsmObj.MassError = massOverzErrorNum * Math.Abs(zNum);
                            PsmObj.MissedCleavage = Int32.Parse(item_interest["Missed Cleavages"]);
                            PsmObj.AlgorithmScore = item_interest["Algorithm Score"];
                            readRowFlag = true;  //have read this PSM in this row    
                            pepTheoryMass = double.Parse(item_interest["Theoretical Mass"]);
                        }

                        ds_Peptide peptideObj = this.searchResultObj.Protein_Dic[ProtName].Peptide_Dic[pepModSeqName];
                        peptideObj.Theoretical_mass = pepTheoryMass;
                        peptideObj.PsmList.Add(PsmObj); //add PSM for this peptide of the specific protein.
                    }//end add psm node 

                } //end each protein in each line.


                rowNum++;
            }
            txtReader.Close();          
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


            string[] itemcheckarray = {"Protein(s)", "Description(s)", "Sequence", "Modified Sequence", "AAs Before", "AAs After", 
                                      "Variable Modifications", "Fixed Modifications", "Confidence [%]", "Validation", "#Validated PSMs"};
            List<string> existing_item = new List<string>();
            for(int i = 0; i < itemcheckarray.GetLength(0); i++ )
            {
                if(this.pepItemName_Dic.ContainsKey(itemcheckarray[i]))
                {
                    existing_item.Add(itemcheckarray[i]);
                }
                else{
                    Console.WriteLine("No {0} item exists.", itemcheckarray[i]);
                }                      
            }

            for (int rowNum = 1; rowNum <= hst.LastRowNum; rowNum++) //read row by row
            {
                hr = (HSSFRow)hst.GetRow(rowNum);
                //Parse protein/peptide info
                string protStr = existing_item.Contains("Protein(s)") == true ? hr.GetCell(this.pepItemName_Dic["Protein(s)"]).ToString() : "";
                string protDescripStr = existing_item.Contains("Description(s)") == true ? hr.GetCell(this.pepItemName_Dic["Description(s)"]).ToString() : "";
                string pepSeqStr = existing_item.Contains("Sequence") == true ? hr.GetCell(this.pepItemName_Dic["Sequence"]).ToString() : "";
                string ModpepSeqStr = existing_item.Contains("Modified Sequence") == true ? hr.GetCell(this.pepItemName_Dic["Modified Sequence"]).ToString() : "";
                string AAsBeforeStr = existing_item.Contains("AAs Before") == true ? hr.GetCell(this.pepItemName_Dic["AAs Before"]).ToString() : "";
                string AAsAfterStr = existing_item.Contains("AAs After") == true ? hr.GetCell(this.pepItemName_Dic["AAs After"]).ToString() : "";
                string VarModStr = existing_item.Contains("Variable Modifications") == true ? hr.GetCell(this.pepItemName_Dic["Variable Modifications"]).ToString() : "";
                string FixModStr = existing_item.Contains("Fixed Modifications") == true ? hr.GetCell(this.pepItemName_Dic["Fixed Modifications"]).ToString() : "";
                double pepScore = existing_item.Contains("Confidence [%]") == true ? double.Parse(hr.GetCell(this.pepItemName_Dic["Confidence [%]"]).ToString()) : -1.0;
                string ValidStr = existing_item.Contains("Validation") == true ? hr.GetCell(this.pepItemName_Dic["Validation"]).ToString() : "";
                int validPsmNum = existing_item.Contains("#Validated PSMs") == true ? Int32.Parse(hr.GetCell(this.pepItemName_Dic["#Validated PSMs"]).ToString()) : 0;


                if (validPsmNum == 0)  //no need to read this peptide without any valid PSM
                    continue;

                ds_Peptide peptideObj = new ds_Peptide();  //Save info into into temp ds_peptide

                peptideObj.Sequence = pepSeqStr;
                peptideObj.Score = pepScore;

                peptideObj.Validation = ValidStr;

                this.StoreModPosList(peptideObj, VarModStr, FixModStr);
                this.GetPepModSeq(peptideObj, pepSeqStr, ModpepSeqStr);
                peptideObj.ModifiedSequence = ModpepSeqStr;  // 12/09 這行覆蓋掉上一行的結果，即把檔案裡提供的mod seq搬過來當作modified sequence
                //change Sequence to ModSequence according to ModPosList information

                //save peptide info into existed proteinObj
                string[] protNames = protStr.Split(';');
                string[] protDescrips = protDescripStr.Split(';');
                string[] AAsBefores = AAsBeforeStr.Split(';');
                string[] AAsAfters = AAsAfterStr.Split(';');

                for (int i = 0; i < protNames.GetLength(0); i++)
                {
                    ds_Peptide pepClone = ds_Peptide.DeepClone<ds_Peptide>(peptideObj);

                    string ProtName = protNames[i].Trim();
                    string protDescrip = protNames[i].Trim();

                    if (AAsBefores.Length - 1 >= i)
                        pepClone.PrevAA = AAsBefores[i].Trim();  //取代peptideObj.PrevAA = AAsBefores[i].Trim();

                    if (AAsAfters.Length - 1 >= i)
                        pepClone.NextAA = AAsAfters[i].Trim();  //取代peptideObj.NextAA = AAsAfters[i].Trim();

                    //only update Protein has appeared in protXls
                    if (this.searchResultObj.Protein_Dic.ContainsKey(ProtName))
                    {
                        ds_Protein proteinObj = this.searchResultObj.Protein_Dic[ProtName];
                        proteinObj.Description = protDescrip;
                        // some mod like Pyrolidone，在pepseq上沒有顯示相對應的Tag，因此造成有相同名稱的peptideId，所以這邊先判斷是否已經存在該peptide，有的話就保留，即只存第一個
                        if (!proteinObj.Peptide_Dic.ContainsKey(pepClone.ModifiedSequence))
                            proteinObj.Peptide_Dic.Add(pepClone.ModifiedSequence, pepClone);
                    }

                }  //end each protein in each row

            }
                  //end reading in each row
                //end reading peptide xls file.
            wk.Clear();
        }

        private void ReadPepTXT(string pepLv_Txt)
        {
            StreamReader txtReader = new StreamReader(pepLv_Txt);
            int colNum = this.DicSaveItemName_TXT(txtReader.ReadLine(), this.pepItemName_Dic);
            string[] itemcheckarray = {"Protein(s)", "Description(s)", "Sequence", "Modified Sequence", "AAs Before", "AAs After", 
                                      "Variable Modifications", "Fixed Modifications", "Confidence [%]", "Validation", "#Validated PSMs"};
            List<string> existing_item = new List<string>();
            for (int i = 0; i < itemcheckarray.GetLength(0); i++)
            {
                if (this.pepItemName_Dic.ContainsKey(itemcheckarray[i]))                
                    existing_item.Add(itemcheckarray[i]);                
                else                
                    Console.WriteLine("No {0} item exists.", itemcheckarray[i]);                
            }

            string line = "";
            int rowNum = 1;
            while ((line = txtReader.ReadLine()) != null)
            {
                string[] elements = line.Replace("\n", "").Replace("\r", "").Split('\t');
                if (elements.GetLength(0) != colNum)
                    continue;

                string protStr = existing_item.Contains("Protein(s)") == true ? elements[this.pepItemName_Dic["Protein(s)"]] : "";
                string protDescripStr = existing_item.Contains("Description(s)") == true ? elements[this.pepItemName_Dic["Description(s)"]] : "";
                string pepSeqStr = existing_item.Contains("Sequence") == true ? elements[this.pepItemName_Dic["Sequence"]] : "";
                string ModpepSeqStr = existing_item.Contains("Modified Sequence") == true ? elements[this.pepItemName_Dic["Modified Sequence"]] : "";
                string AAsBeforeStr = existing_item.Contains("AAs Before") == true ? elements[this.pepItemName_Dic["AAs Before"]] : "";
                string AAsAfterStr = existing_item.Contains("AAs After") == true ? elements[this.pepItemName_Dic["AAs After"]] : "";
                string VarModStr = existing_item.Contains("Variable Modifications") == true ? elements[this.pepItemName_Dic["Variable Modifications"]] : "";
                string FixModStr = existing_item.Contains("Fixed Modifications") == true ? elements[this.pepItemName_Dic["Fixed Modifications"]] : "";
                double pepScore = existing_item.Contains("Confidence [%]") == true ? double.Parse(elements[this.pepItemName_Dic["Confidence [%]"]]) : -1.0;
                string ValidStr = existing_item.Contains("Validation") == true ? elements[this.pepItemName_Dic["Validation"]] : "";
                int validPsmNum = existing_item.Contains("#Validated PSMs") == true ? Int32.Parse(elements[this.pepItemName_Dic["#Validated PSMs"]]) : 0;

                if (validPsmNum == 0)  //no need to read this peptide without any valid PSM
                    continue;

                ds_Peptide peptideObj = new ds_Peptide();  //Save info into into temp ds_peptide
                peptideObj.Sequence = pepSeqStr;
                peptideObj.Score = pepScore;
                peptideObj.Validation = ValidStr;

                this.StoreModPosList(peptideObj, VarModStr, FixModStr);
                this.GetPepModSeq(peptideObj, pepSeqStr, ModpepSeqStr);
                peptideObj.ModifiedSequence = ModpepSeqStr;  // 12/09 這行覆蓋掉上一行的結果，即把檔案裡提供的mod seq搬過來當作modified sequence
                //change Sequence to ModSequence according to ModPosList information
                //save peptide info into existed proteinObj
                string[] protNames = protStr.Split(';');
                string[] protDescrips = protDescripStr.Split(';');
                string[] AAsBefores = AAsBeforeStr.Split(';');
                string[] AAsAfters = AAsAfterStr.Split(';');

                for (int i = 0; i < protNames.GetLength(0); i++)
                {
                    ds_Peptide pepClone = ds_Peptide.DeepClone<ds_Peptide>(peptideObj);

                    string ProtName = protNames[i].Trim();
                    string protDescrip = protNames[i].Trim();

                    if (AAsBefores.Length - 1 >= i)
                        pepClone.PrevAA = AAsBefores[i].Trim();  //取代peptideObj.PrevAA = AAsBefores[i].Trim();

                    if (AAsAfters.Length - 1 >= i)
                        pepClone.NextAA = AAsAfters[i].Trim();  //取代peptideObj.NextAA = AAsAfters[i].Trim();

                    //only update Protein has appeared in protXls
                    if (this.searchResultObj.Protein_Dic.ContainsKey(ProtName))
                    {
                        ds_Protein proteinObj = this.searchResultObj.Protein_Dic[ProtName];
                        proteinObj.Description = protDescrip;
                        // some mod like Pyrolidone，在pepseq上沒有顯示相對應的Tag，因此造成有相同名稱的peptideId，所以這邊先判斷是否已經存在該peptide，有的話就保留，即只存第一個
                        if (!proteinObj.Peptide_Dic.ContainsKey(pepClone.ModifiedSequence))
                            proteinObj.Peptide_Dic.Add(pepClone.ModifiedSequence, pepClone);
                    }

                }  //end each protein in each row
                rowNum++;
            }
            txtReader.Close();        
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
                    
                bool needsplit = false;
                if (protGroupStr.IndexOf(", ", 0) >= 0)
                {
                    needsplit = true;
                }
                string temp_protNames = needsplit == true ? protGroupStr.Replace(", ", ";") : protGroupStr;  // 12/13 先用置換 把 ", " 換成 ";" ，因為檔案中有可能會有其他逗號使用，惟有接著空格的逗號: ", " 才能確定是分隔不同protNames
                string[] protNames = needsplit == true ? temp_protNames.Split(';') : temp_protNames.Split('*');      // 12/13 原本是',' 但因為上一行換成";" 所以相應改變這邊用來分割的字符

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
            wk.Clear();
        }

        private void ReadProtTXT(string protLv_Txt)
        {
            StreamReader txtReader = new StreamReader(protLv_Txt);
            string line = "";
            int colNum = this.DicSaveItemName_TXT(txtReader.ReadLine(), this.protItemName_Dic);
            int rowNum = 1;
            while ((line = txtReader.ReadLine()) != null)
            {
                string[] elements = line.Replace("\n", "").Replace("\r", "").Split('\t');
                if (elements.GetLength(0) != colNum)
                    continue;

                string protGroupStr = elements[this.protItemName_Dic["Protein Group"]] != null ? elements[this.protItemName_Dic["Protein Group"]] : "";
                double protGroupProb = elements[this.protItemName_Dic["Confidence [%]"]] != null ? double.Parse(elements[this.protItemName_Dic["Confidence [%]"]]) : -1.0F;
                string protGroupValidstr = elements[this.protItemName_Dic["Validation"]] != null ? elements[this.protItemName_Dic["Validation"]] : "";
                int validPepNum = elements[this.protItemName_Dic["#Validated Peptides"]] != null ? Int32.Parse(elements[this.protItemName_Dic["#Validated Peptides"]]) : 0;
                int validPsmNum = elements[this.protItemName_Dic["#Validated PSMs"]] != null ? Int32.Parse(elements[this.protItemName_Dic["#Validated PSMs"]]) : 0;
                if ((validPepNum == 0) || (validPsmNum == 0))
                    continue;    //no need to read this protein without any valid peptide/PSM

                bool needsplit = false;
                if (protGroupStr.IndexOf(", ", 0) >= 0)                
                    needsplit = true;

                string temp_protNames = needsplit == true ? protGroupStr.Replace(", ", ";") : protGroupStr;  // 12/13 先用置換 把 ", " 換成 ";" ，因為檔案中有可能會有其他逗號使用，惟有接著空格的逗號: ", " 才能確定是分隔不同protNames
                string[] protNames = needsplit == true ? temp_protNames.Split(';') : temp_protNames.Split('*');      // 12/13 原本是',' 但因為上一行換成";" 所以相應改變這邊用來分割的字符

                List<string> protNameList = new List<string>();

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
                    this.searchResultObj.Protein_Dic[tmpProtName].Validation = protGroupValidstr;
                }
                this.searchResultObj.ProtGroupProb_Dic.Add(rowNum, protGroupProb);
                this.searchResultObj.ProtGroupName_Dic.Add(rowNum, protNameList);

                rowNum++;
            }
            txtReader.Close();
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

        //--- 讀取txt格式的tab分隔檔案的第一列，取得各個column的名稱與順序 ---//
        private int DicSaveItemName_TXT(string line, Dictionary<string, int> ItemName_Dic)
        {
            string[] cells = line.Replace("\r","").Replace("\n","").Split('\t');
            int ColnumNum = cells.GetLength(0);

            for (int i = 1; i <= (ColnumNum - 1); i++) //column 0 is row number
            {
                string itemStr = cells[i] != null ? cells[i] : "";

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
            return ColnumNum;
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
                    AlphaStr = "[160]";
                    break;

                default:
                    AlphaStr = "[0]";
                    break;
            }

            return AlphaStr;
        }

    }
}

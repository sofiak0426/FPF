using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;

namespace ResultReader
{
    public class IdpickerReader
    {
        private ds_SearchResult searchResultObj = new ds_SearchResult();
        private Dictionary<string, int> psmItemName_Dic = new Dictionary<string, int>();
        private Dictionary<string, int> pepItemName_Dic = new Dictionary<string, int>();
        private Dictionary<string, int> protItemName_Dic = new Dictionary<string, int>();
        private char splitsign;
        // 記錄一下titleParser處理title的結果是屬於which type
        private List<string> PSMtitleTypeLi = new List<string>();


        public ds_SearchResult ReadFiles(string protTsvPath, string pepTsvPath, string psmTsvPath)
        {
            this.searchResultObj.Source = SearchResult_Source.Idpicker;
            if ((psmTsvPath != "") && (pepTsvPath != "") && (protTsvPath != ""))
            {
                this.ReadProtTsv(protTsvPath);
                this.ReadPepTsv(pepTsvPath);
                this.ReadPsmTsv(psmTsvPath);
                this.searchResultObj.Filter_Peptide(0.0F);
                this.searchResultObj.RefreshPepProt_Dic();
                this.searchResultObj.RefreshPepProtGroup_Dic();
            }
            return this.searchResultObj;
        }

        private void ReadProtTsv(string protTsvPath)
        {
            this.splitsign = Path.GetExtension(protTsvPath) == ".TSV" ? '\t' : ',';
            StreamReader sr = new StreamReader(protTsvPath);
            string firstline = sr.ReadLine();
            this.protItemName_Dic = this.SaveItemNameDic(firstline);
            int lineNum = 0;
            string line;
            while((line = sr.ReadLine()) != null)
            {
                lineNum++;
                string[] cells = line.Split(this.splitsign);

                //string protGroupStr = cells[this.protItemName_Dic["Protein Group"]];
                string protGroupStr = cells[this.protItemName_Dic["Accession"]];
                string descStr = cells[this.protItemName_Dic["Description"]];
                double coverage = cells[this.protItemName_Dic["Coverage"]] == "" ? 0 : Convert.ToDouble(cells[this.protItemName_Dic["Coverage"]]);
                int num_Pep = Convert.ToInt32(cells[this.protItemName_Dic["Distinct Peptides"]]);
                int num_Matches = Convert.ToInt32(cells[this.protItemName_Dic["Distinct Matches"]]);
                int num_Spectra = Convert.ToInt32(cells[this.protItemName_Dic["Filtered Spectra"]]);

                if (num_Pep == 0)
                    continue;

                //check是否group內有多個proteins
                bool needsplit = false;
                if (protGroupStr.IndexOf(",", 0) >= 0)                
                    needsplit = true;               

                string temp_protNames = protGroupStr.Replace("|", "");
                temp_protNames = temp_protNames.Replace("\"", "");  // 好像從ReadLine進來的部分 有些protein accessions就會有"或\符號在裡面? 怪
                temp_protNames = temp_protNames.Replace("\\", "");

                string[] protNames = needsplit == true ? temp_protNames.Split(',') : temp_protNames.Split('$');
                List<string> protNameList = new List<string>();
                for (int i = 0; i < protNames.GetLength(0); i++) //each protein in one row
                {
                    string tmpProtName = protNames[i].Trim();
                    ds_Protein ProteinNode = new ds_Protein();
                    protNameList.Add(tmpProtName);
                    ProteinNode.ProtID = tmpProtName;
                    ProteinNode.Description = descStr;
                    //proteinDic
                    if (!this.searchResultObj.Protein_Dic.ContainsKey(tmpProtName))
                        this.searchResultObj.Protein_Dic.Add(tmpProtName, ProteinNode);

                    //protein Validation
                    searchResultObj.Protein_Dic[tmpProtName].Validation = "";
                }
                searchResultObj.ProtGroupProb_Dic.Add(lineNum, 0);
                searchResultObj.ProtGroupName_Dic.Add(lineNum, protNameList);   // 是用檔案第幾行作為索引值嗎?                
            }
            sr.Close();
        }

        private void ReadPepTsv(string pepTsvPath)
        {
            //string[] itemcheckArr = {"Sequence", "Molecular Weight", "Proteins", "Protein Accessions"};
            this.splitsign = Path.GetExtension(pepTsvPath) == ".TSV" ? '\t' : ',';
            StreamReader sr = new StreamReader(pepTsvPath);
            string firstline = sr.ReadLine();
            this.pepItemName_Dic = this.SaveItemNameDic(firstline);
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                string[] cells = line.Split(this.splitsign);               
                
                string seqStr = this.pepItemName_Dic.ContainsKey("Sequence") ? cells[this.pepItemName_Dic["Sequence"]].Split(' ')[0] : "";
                string[] _seqArr = cells[this.pepItemName_Dic["Sequence"]].Split(' ');
                if (_seqArr.GetLength(0) > 2)
                    seqStr = _seqArr[0];

                int num_Spectra = this.pepItemName_Dic.ContainsKey("Filtered Spectra") ? Convert.ToInt32(cells[this.pepItemName_Dic["Filtered Spectra"]]) : 0;
                int num_Proteins = this.pepItemName_Dic.ContainsKey("Proteins") ? Convert.ToInt32(cells[this.pepItemName_Dic["Proteins"]]) : 0;
                string protStr = this.pepItemName_Dic.ContainsKey("Protein Accessions") ? cells[this.pepItemName_Dic["Protein Accessions"]] : "";
                double mw = this.pepItemName_Dic.ContainsKey("Molecular Weight") ? Convert.ToDouble(cells[this.pepItemName_Dic["Molecular Weight"]]) : 0;
                double mm = this.pepItemName_Dic.ContainsKey("Monoisotopic Mass") ? Convert.ToDouble(cells[this.pepItemName_Dic["Monoisotopic Mass"]]) : 0;

                if (num_Spectra == 0 || num_Proteins == 0 || this.searchResultObj.PepProt_Dic.ContainsKey(seqStr))
                    continue;

                

                string temp_protNames = protStr.Replace("|", "");
                temp_protNames = temp_protNames.Replace("\"", "");  // 好像從ReadLine進來的部分 有些protein accessions就會有"或\符號在裡面? 怪
                temp_protNames = temp_protNames.Replace("\\", "");
                string[] protNames = temp_protNames.Split(',');
                List<string> protNameList = new List<string>();
                for (int i = 0; i < protNames.GetLength(0); i++) //each protein in one row
                {
                    string tmpProtName = protNames[i].Trim();
                    //only update Protein's peptide list for those proteins has appeared in protTsv
                    // 02/06 is it possible that some proteins don't exist in protTSV? incomprehensive codes in IDPicker for handling this? 
                    //       實際上有這狀況: protein accessions有6 proteins，但經過和Prot_Dic對照後只剩2個保留下來
                    if (this.searchResultObj.Protein_Dic.ContainsKey(tmpProtName))
                    {
                        ds_Protein proteinObj = this.searchResultObj.Protein_Dic[tmpProtName]; // claimed by reference?
                        ds_Peptide peptideObj = new ds_Peptide();  //Save info into into temp ds_peptide
                        peptideObj.Sequence = seqStr;
                        peptideObj.Theoretical_mass = mw;
                        
                        if (proteinObj.Peptide_Dic.ContainsKey(peptideObj.Sequence))
                            break;

                        
                        proteinObj.Peptide_Dic.Add(peptideObj.Sequence, peptideObj);

                        protNameList.Add(tmpProtName);  
                    }
                }

                if (!this.searchResultObj.PepProt_Dic.ContainsKey(seqStr))
                {
                    this.searchResultObj.PepProt_Dic.Add(seqStr, protNameList);    // 借用PepProt_Dic: pep => prot，提供psmTSV讀取時以pep seq對照到含有該pep的protein nodes
                }
            }
            sr.Close();
        }

        private void ReadPsmTsv(string psmTsvPath)
        {

            this.splitsign = Path.GetExtension(psmTsvPath) == ".TSV" ? '\t' : ',';
            StreamReader sr = new StreamReader(psmTsvPath);
            string firstline = sr.ReadLine();
            this.psmItemName_Dic = this.SaveItemNameDic(firstline);
            //--- 測試用的psmTSV檔案 用MS Excel打開會有3行 在跟後續資料列不同的column裡有數據，且前兩列內容一樣，應該只是一些properties for whole psm data(或可用作全部讀取整理完之後的驗證?)，在此先跳過這三行，之後如果發現有需要取得這部分數據的話再另行修改
            //sr.ReadLine();
            //sr.ReadLine();
            //sr.ReadLine();
            string line;
            // 每一row是一張spectrum 會有多個spectra對應到同個peptide(即"sequence"的內容一樣)
            // IDPicker測試檔中psmTSV裡面沒有"Protein" level的資訊，僅有"Sequence"，所以在讀取pepTSV時也另外存好pep => prot的索引
            long lineCount = 0;
            while ((line = sr.ReadLine()) != null)
            {
                lineCount++;                                
                string[] cells = line.Split(this.splitsign);

                string seqStr = this.psmItemName_Dic.ContainsKey("Sequence") ? cells[this.psmItemName_Dic["Sequence"]] : "";
                string _charge = this.psmItemName_Dic.ContainsKey("Charge") ? cells[this.psmItemName_Dic["Charge"]] : "";
                string _mz = this.psmItemName_Dic.ContainsKey("Precursor m/z") ? cells[this.psmItemName_Dic["Precursor m/z"]] : "";
                string _mr = this.psmItemName_Dic.ContainsKey("Observed Mass") ? cells[this.psmItemName_Dic["Observed Mass"]] : "";
                string _me = this.psmItemName_Dic.ContainsKey("Mass Error") ? cells[this.psmItemName_Dic["Mass Error"]] : "";
                string _et = this.psmItemName_Dic.ContainsKey("Scan Time") ? cells[this.psmItemName_Dic["Scan Time"]] : "";      
                string _qvalue = this.psmItemName_Dic.ContainsKey("Q Value") ? cells[this.psmItemName_Dic["Q Value"]] : "";
                //title的選擇，由於IDPicker操作時可以選擇輸出時要分層 或是把分層資訊放進spectrum title，所以目前依兩種型式有兩種取得title的方法
                string title = this.psmItemName_Dic.ContainsKey("Group/Source/Spectrum") ? cells[this.psmItemName_Dic["Group/Source/Spectrum"]] : cells[this.psmItemName_Dic["Spectrum"]]; 

                int charge = _charge == "" ? 0 : Convert.ToInt32(_charge);
                double mz = _mz == "" ? 0 : Convert.ToDouble(_mz);
                double mr = _mr == "" ? 0 : Convert.ToDouble(_mr);
                double me = _me == "" ? 0 : Convert.ToDouble(_me);
                float et = _et == "" ? 0 : Convert.ToSingle(_et) * 60;  // 02/14 檔案內似乎是以分鐘為單位
                double qvalue = _qvalue == "" ? 0 : Convert.ToDouble(_qvalue);
                if (charge == 0 && mz == 0 && mr == 0 && me == 0)
                    continue;

                // 用sequence資訊 查找PepProt_Dic 讓所有包含此peptide的protein nodes下面的peptide node新增此psm node
                if (!this.searchResultObj.PepProt_Dic.ContainsKey(seqStr))
                    continue;               
               
                // 取得含有此pep的protein name
                List<string> corrProtLi = this.searchResultObj.PepProt_Dic[seqStr].ToList();
                ds_PSM PsmObj = new ds_PSM(this.searchResultObj.Source);
                PsmObj.Charge = charge;
                PsmObj.MassError = me;
                PsmObj.Precursor_mz = mz;
                PsmObj.Pep_exp_mass = mr;
                PsmObj.ElutionTime = et;
                PsmObj.Score = qvalue; // ------>先暫時用qvalue帶入，因為還不確定IDPicker到底有哪些分數，先用這個大於0的值，或取個-log
                PsmObj.Peptide_Scan_Title = title.Trim();
                TitleParser ttp = new TitleParser();
                ds_ScanTitleInfo titleObj = ttp.parse(PsmObj.Peptide_Scan_Title);
                PsmObj.Peptide_Scan_Title = titleObj.rawDataName;
                PsmObj.scanNumber = titleObj.scanNum;

                if (!PSMtitleTypeLi.Contains(titleObj.titleType))
                    PSMtitleTypeLi.Add(titleObj.titleType); 

                // 上面完成此PSM node，對protein nodes下面的這個peptide node添增此PSM node的Deep Clone
                foreach(string protName in corrProtLi){
                    ds_Protein tmpProtObj = this.searchResultObj.Protein_Dic[protName];
                    if (!tmpProtObj.Peptide_Dic.ContainsKey(seqStr))
                        continue;

                    if (!tmpProtObj.Peptide_Dic[seqStr].PsmList.Contains(PsmObj))
                    {
                        //ds_Protein tmpProtObj = this.searchResultObj.Protein_Dic[protName];
                        tmpProtObj.Peptide_Dic[seqStr].PsmList.Add(ds_PSM.DeepClone<ds_PSM>(PsmObj));
                    }
                }
            }
            sr.Close();
            this.searchResultObj.PepProt_Dic.Clear();
        }

        private Dictionary<string, int> SaveItemNameDic(string fileContent)
        {
            Dictionary<string, int> tempDic = new Dictionary<string, int>();
            string[] cells = fileContent.Split(this.splitsign);

            for (int i = 0; i < cells.GetLength(0); i ++)
            {
                if (!tempDic.ContainsKey(cells[i]))
                    tempDic.Add(cells[i], i);
            }
            return tempDic;
        }

    }
}

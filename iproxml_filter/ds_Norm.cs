using System;
using System.Collections.Generic;
using System.Linq;
using ResultReader;

namespace iproxml_filter
{
    class ds_Norm
    {
        private List<double> _bgNormRatioLi = new List<double>(); //The ratio that each channel should multiply by during normalization (obtained from channel median intensity)
        private List<(string, string)> _bgNormKeyLi = new List<(string, string)>(); //Specify keywords to identify background proteins. Key: "PRE" for prefixes and "SUF" for suffixes; Value: the keyword
        
        //Add background prefixes or suffixes to the _bgProtKeywordLi
        public void AddBgProtKey(String[] bgProtKeyArr)
        {
            foreach (string bgProtKeyStr in bgProtKeyArr)
            {
                if (bgProtKeyStr.EndsWith('-') && !bgProtKeyStr.StartsWith('-')) //Prefix
                    this._bgNormKeyLi.Add(("PRE", bgProtKeyStr.Substring(0, bgProtKeyStr.Length - 1)));
                else if (bgProtKeyStr.StartsWith('-')) //Suffix
                    this._bgNormKeyLi.Add(("SUF", bgProtKeyStr.Substring(1)));
                else
                    throw new ApplicationException(String.Format("You specified background keywords in the wrong format: {0}", bgProtKeyStr));
            }           
        }

        //Calculate ratio for normalization from median intensity of all valid PSMs
        public void GetChannelMed(ds_Parameters parametersObj, ds_SearchResult dbSearchResult)
        {
            //Check if normalization is needed; if no, then specify normalization ratios to 1
            if (this._bgNormKeyLi.Count == 0)
            {
                for (int i = 0; i < parametersObj.ChannelCnt - 1; i++)
                    _bgNormRatioLi.Add(1.0);
                return;
            }

            //Setup 
            List<List<double>> chanAllIntenLi = new List<List<double>>();
            for (int i = 0; i < parametersObj.ChannelCnt; i++)
                chanAllIntenLi.Add(new List<double>());

            //Puah all intensities of valid PSMs into the list chanAllIntenLi
            foreach (KeyValuePair<string, ds_Protein> prot in dbSearchResult.Protein_Dic)
            {
                //check if the protein is a background protein
                bool isBg = false;
                foreach ((string ind, string key) bgProtKey in this._bgNormKeyLi)
                {
                    if (bgProtKey.ind == "PRE" && prot.Key.StartsWith(bgProtKey.key)) //prefix
                    {
                        isBg = true;
                        break;
                    }
                    else if (bgProtKey.ind == "SUF" && prot.Key.EndsWith(bgProtKey.key)) //suffix
                    {
                        isBg = true;
                        break;
                    }
                }
                if (isBg == false)
                    continue;

                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        if (FPFActions.PsmIsValid(psm, pep.Value, prot.Value, parametersObj.DbFdr001Prob, parametersObj.DecoyPrefixArr) == 1)
                        {
                            for (int i = 0; i < parametersObj.ChannelCnt; i++)
                                chanAllIntenLi[i].Add(psm.libra_ChanIntenDi.Values.ToList()[i]);
                        }
                    }
                }
            }
            
            //Find median of each channel
            List<double> chanMedLi = new List<double>();
            foreach (List<double> chanAllInten in chanAllIntenLi)
            {
                chanAllInten.Sort();
                double median = 0;
                if (chanAllInten.Count % 2 == 0)
                    median = (chanAllInten[chanAllInten.Count / 2 - 1] + chanAllInten[chanAllInten.Count / 2]) / 2;
                else
                    median = chanAllInten[(chanAllInten.Count - 1) / 2];
                chanMedLi.Add(median);
            }
            for (int i = 0; i < parametersObj.ChannelCnt; i++)
            {
                if (i != parametersObj.RefChannel - 1)
                    this._bgNormRatioLi.Add(chanMedLi[parametersObj.RefChannel - 1] / chanMedLi[i]);
            }
        }

        //Calculate normalized intensities
        public List<double> GetNormIntenLi(ds_Parameters parametersObj, List<double> psmOrigIntenLi)
        {
            List<double> psmNormIntenLi = new List<double>();
            int ratioIndex = 0;
            for (int i = 0; i < parametersObj.ChannelCnt; i++)
            {
                if (i == parametersObj.RefChannel - 1)
                    psmNormIntenLi.Add(psmOrigIntenLi[i]);
                else
                {
                    psmNormIntenLi.Add(psmOrigIntenLi[i] * _bgNormRatioLi[ratioIndex]);
                    ratioIndex++;
                }
            }
            return psmNormIntenLi;
        }
    }
}

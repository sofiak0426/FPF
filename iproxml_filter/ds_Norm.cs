using System;
using System.Collections.Generic;
using System.Linq;
using ResultReader;

namespace FPF
{
    class ds_Norm
    {
        private List<double> _bgNormRatioLi = new List<double>(); //List storing the ratio that each channel should multiply by during normalization (obtained from median reporter ion intensity of each channel)
        private List<(string, string)> _bgNormKeyLi = new List<(string, string)>(); //Specify protein-ID keywords to identify background proteins. Key: "PRE" for prefixes and "SUF" for suffixes; Value: the protein-name keyword


        /// <summary>
        /// Adding background prefixes or suffixes to _bgNormKeyLi
        /// </summary>
        /// <param name="bgProtKeyArr">Array of strings containing background protein keywords from parameter file</param>
        public void AddBgProtKey(String[] bgProtKeyArr)
        {
            foreach (string bgProtKeyStr in bgProtKeyArr)
            {
                if (bgProtKeyStr.EndsWith('-') && !bgProtKeyStr.StartsWith('-')) //Prefix
                    this._bgNormKeyLi.Add(("PRE", bgProtKeyStr.Substring(0, bgProtKeyStr.Length - 1)));
                else if (bgProtKeyStr.StartsWith('-')) //Suffix
                    this._bgNormKeyLi.Add(("SUF", bgProtKeyStr.Substring(1)));
                else
                    throw new ApplicationException(String.Format("Error: you specified background keywords in the wrong format: {0}", bgProtKeyStr));
            }           
        }

        /// <summary>
        /// Calculate normalization ratio from median reporter ion intensity of all valid background PSMs
        /// </summary>
        public void GetChannelMed(ds_Parameters parametersObj, ds_SearchResult dbSearchResult)
        {
            //Check if normalization is needed; if not, specify normalization ratios to 1 and return
            if (this._bgNormKeyLi.Count == 0)
            {
                for (int i = 0; i < parametersObj.ChannelCnt - 1; i++)
                    _bgNormRatioLi.Add(1.0);
                return;
            }

            //chanAllIntenLi is an array that contains n rows (one row for each channel). 
            //Each row contains reporter ion intensities of all valid background PSMs (for that particular channel).
            List<List<double>> chanAllIntenLi = new List<List<double>>(); 
            for (int i = 0; i < parametersObj.ChannelCnt; i++)
                chanAllIntenLi.Add(new List<double>());

            //Puah intensities of valid background PSMs channel by channel into each row in chanAllIntenLi
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
                        if (FPFActions.PsmIsValid(psm, pep.Value, prot.Value, parametersObj.DbFdr001Prob, parametersObj.DecoyPrefixArr) != 1) //invalid PSM or with zero reporter ion intenstiy
                            continue;
                        for (int i = 0; i < parametersObj.ChannelCnt; i++)
                            chanAllIntenLi[i].Add(psm.libra_ChanIntenDi.Values.ToList()[i]);
                    }
                }
            }
            
            //Find median of each channel
            List<double> chanMedLi = new List<double>();
            foreach (List<double> chanAllInten in chanAllIntenLi)
            {
                chanAllInten.Sort();
                double median;
                if (chanAllInten.Count % 2 == 0)
                    median = (chanAllInten[chanAllInten.Count / 2 - 1] + chanAllInten[chanAllInten.Count / 2]) / 2;
                else
                    median = chanAllInten[(chanAllInten.Count - 1) / 2];
                chanMedLi.Add(median);
            }

            //Calculate normalization ratio
            for (int i = 0; i < parametersObj.ChannelCnt; i++)
            {
                if (i != parametersObj.RefChannel - 1)
                    this._bgNormRatioLi.Add(chanMedLi[parametersObj.RefChannel - 1] / chanMedLi[i]);
            }
        }

        /// <summary>
        /// Calculate the normalized reporter ion intensities of a single PSM.
        /// </summary>
        /// <param name="psmOrigIntenLi">The original reporter ion intensities of a single PSM. </param>
        /// <returns></returns>
        public List<double> GetNormIntenLi(ds_Parameters parametersObj, List<double> psmOrigIntenLi)
        {
            List<double> psmNormIntenLi = new List<double>();
            int ratioIndex = 0;
            for (int i = 0; i < parametersObj.ChannelCnt; i++)
            {
                if (i == parametersObj.RefChannel - 1) //reference channel
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

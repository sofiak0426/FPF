using System;
using System.Collections.Generic;
using System.Linq;
using ResultReader;

namespace FPF
{
    class ds_Norm
    {
        private List<double> _bgNormFactorLi = new List<double>(); //List storing the ratio that each channel should multiply by during normalization (obtained from median reporter ion intensity of each channel)
        private List<(string, string)> _stdNormKeywordLi = new List<(string, string)>(); //Specify protein-ID keywords to identify background proteins. 0: "PRE" for prefixes and "SUF" for suffixes; 1: the protein-name keyword

        /// <summary>
        /// Adding standard protein prefixes or suffixes to _stdNormKeywordLi
        /// </summary>
        /// <param name="stdProtNameKeywordArr">Array of strings containing background protein keywords from parameter file</param>
        public void AddStdProtKeyword(String[] stdProtNameKeywordArr)
        {
            foreach (string stdProtNameKeywordStr in stdProtNameKeywordArr)
            {
                if (stdProtNameKeywordStr.EndsWith('-') && !stdProtNameKeywordStr.StartsWith('-')) //Prefix
                    this._stdNormKeywordLi.Add(("PRE", stdProtNameKeywordStr.Substring(0, stdProtNameKeywordStr.Length - 1)));
                else if (stdProtNameKeywordStr.StartsWith('-')) //Suffix
                    this._stdNormKeywordLi.Add(("SUF", stdProtNameKeywordStr.Substring(1)));
                else
                    throw new ApplicationException(String.Format("Error: you specified standard-protein keywords in the wrong format: {0}", stdProtNameKeywordStr));
            }           
        }

        /// <summary>
        /// Calculate normalization ratio from median reporter ion intensity of all valid background PSMs
        /// </summary>
        public void GetChannelMed(ds_Parameters parametersObj, ds_SearchResult dbSearchResult)
        {
            //Check if normalization is needed; if not, specify normalization ratios to 1 and return
            if (this._stdNormKeywordLi.Count == 0)
            {
                for (int i = 0; i < parametersObj.ChannelCnt - 1; i++)
                    _bgNormFactorLi.Add(1.0);
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
                bool isBg = true;
                foreach ((string ind, string keyword) stdProtKeyword in this._stdNormKeywordLi)
                {
                    if (stdProtKeyword.ind == "PRE" && prot.Key.StartsWith(stdProtKeyword.keyword)) //prefix
                    {
                        isBg = false;
                        break;
                    }
                    else if (stdProtKeyword.ind == "SUF" && prot.Key.EndsWith(stdProtKeyword.keyword)) //suffix
                    {
                        isBg = false;
                        break;
                    }
                }
                if (isBg == false)
                    continue;

                //If the protein is background, add intensities to chanAllIntenLi
                foreach (KeyValuePair<string, ds_Peptide> pep in prot.Value.Peptide_Dic)
                {
                    foreach (ds_PSM psm in pep.Value.PsmList)
                    {
                        if (FPFActions.PsmIsValid(psm, pep.Value, prot.Value, parametersObj.IdsFdr001Prob, parametersObj.DecoyKeywordLi) != 1) //invalid PSM or with zero reporter ion intenstiy
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

            //Calculate normalization factor
            for (int i = 0; i < parametersObj.ChannelCnt; i++)
            {
                if (i != parametersObj.RefChannel - 1)
                    this._bgNormFactorLi.Add(chanMedLi[parametersObj.RefChannel - 1] / chanMedLi[i]);
                else
                    this._bgNormFactorLi.Add(1);
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
            for (int i = 0; i < parametersObj.ChannelCnt; i++)
                psmNormIntenLi.Add(psmOrigIntenLi[i] * _bgNormFactorLi[i]);
            return psmNormIntenLi;
        }
    }
}

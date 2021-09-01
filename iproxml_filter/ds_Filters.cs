using System;
using System.Collections.Generic;


namespace iproxml_filter
{

    public class ds_Filters //list for selected ranges for each feature
    {
        public readonly List<string> featureNameLi = new List<string> {"Charge", "Mass", "Peptide Length", "Average Reporter Ion Intensity",
        "Intra-Peptide Euclidean Distance", "Intra-Protein Euclidean Distance", "Number of PTMs", "PTM Ratio","Absolute Mass Difference",
        "Absolute Precursor Mz Difference","Dot Product","DeltaD","Number of Hits","Mean of Dot Products of the Hits",
        "Standard Deviation of Dot Products of the Hits","F-value"};

        //Key: feature name; Value: filter ranges for the feature
        private Dictionary<string, List<(double lowerLim, double upperLim)>> _filtDic = new Dictionary<string, List<(double lowerLim, double upperLim)>>();

        public Dictionary<string, List<(double lowerLim, double upperLim)>> FiltDic
        {
            get { return _filtDic; }
        }

        public void AddFilter(string feature, (double lowerLim, double upperLim) featlim)
        {
            if (!_filtDic.ContainsKey(feature))  //if it is the first filter of the feature, create new dic item
                this._filtDic.Add(feature, new List<(double lowerLim, double upperLim)>());
            _filtDic[feature].Add(featlim);
        }
    }

}

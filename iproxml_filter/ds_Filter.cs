using System;
using System.Collections.Generic;


namespace iproxml_filter
{
    public class ds_Filter //list for selected ranges for each feature
    {
        public static readonly Dictionary <string, string> featAndTypeDic = new Dictionary<string, string>{
            {"Charge","int" }, 
            {"Mass","double"}, 
            {"Peptide Length","int"},
            {"Average Intensity","double" },
            {"Intra-Peptide Euclidean Distance" ,"double"},
            {"Intra-Protein Euclidean Distance", "double"}
        };

        //Key: feature name; Value: filter ranges for the feature
        private Dictionary<string, List<(double lowerLim, double upperLim)>> _filtDic = new Dictionary<string, List<(double lowerLim, double upperLim)>>();

        public List<(double lowerLim, double upperLim)> GetFiltRange (string feature)
        {
            return _filtDic[feature];
        }

        public void AddFilter(string feature, (double lowerLim, double upperLim) featlim)
        {
            if (!_filtDic.ContainsKey(feature))  //if it is the first filter of the feature, create new dic item
                this._filtDic.Add(feature, new List<(double lowerLim, double upperLim)>());
            _filtDic[feature].Add(featlim);
        }
    }

}

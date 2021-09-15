using System.Collections.Generic;


namespace FPF
{

    public class ds_Filters
    {
        //A dictionary that stores whether each feature in the filter is specified by user or not
        //Key: Feature name in param file; Value: if the param is specified by the user
        private Dictionary<string, bool> _featureIsSetDic = new Dictionary<string, bool>{
            {"Charge", false},
            {"Mass", false},
            {"Peptide Length", false},
            {"Average Reporter Ion Intensity", false},
            {"Intra-Peptide Euclidean Distance", false},
            {"Intra-Protein Euclidean Distance", false},
            {"Number of PTMs", false},
            {"PTM Ratio", false},
            {"Absolute Mass Difference", false},
            {"Absolute Precursor Mz Difference", false},
            {"Dot Product", false},
            {"DeltaD", false},
            {"Number of Hits", false},
            {"Mean of Dot Products of the Hits", false},
            {"Standard Deviation of Dot Products of the Hits", false},
            {"F-value", false}
        };

        //Key: feature name; Value: for each feature, list of ranges (lowerLim, upperLim) in which PSMs should be filtered out
        private Dictionary<string, List<(double lowerLim, double upperLim)>> _filtDic = new Dictionary<string, List<(double lowerLim, double upperLim)>>();

        //Key: Feature name in param file; Value: if the param is specified by the user
        public Dictionary<string, bool> FeatureIsSetDic
        {
            get { return _featureIsSetDic; }
        }

        /// <summary>
        /// Key: feature name; Value: for each feature, list of ranges (lowerLim, upperLim) in which PSMs should be filtered out
        /// </summary>
        public Dictionary<string, List<(double lowerLim, double upperLim)>> FiltDic
        {
            get { return _filtDic; }
        }

        /// <summary>
        /// Check whether the current feature name in the param file corresponds to one of the the correct feature names in the dictionary.
        /// True: feature name is correct; False: there is no corresponding feature name
        /// </summary>
        public bool ValidateFeatureName(string paramNameInParamFile)
        {
            return _featureIsSetDic.ContainsKey(paramNameInParamFile);
        }

        /// <summary>
        /// If a filter of a feature is specified by user, change the item value (whose key is the feature name) in _featureIsSetDic to true
        /// </summary>
        public void SetFeatureAsTrue(string paramName)
        {
            _featureIsSetDic[paramName] = true;
        }

        /// <summary>
        /// Check if filters for this feature are specified by the user or not
        /// </summary>
        /// <returns></returns>
        public bool GetFeatureIsSet(string paramName)
        {
            return _featureIsSetDic[paramName];
        }

        /// <summary>
        /// Adds a new filter range (in which PSMs should be filtered out) to a particular feature
        /// </summary>
        /// <param name="feature">The name of feature</param>
        /// <param name="featlim">Lower limit and upper limit of the filter range</param>
        public void AddFilter(string feature, (double lowerLim, double upperLim) featlim)
        {
            if (!_filtDic.ContainsKey(feature))  //If it is the first filter range of the feature, create new item in _filtDic
                this._filtDic.Add(feature, new List<(double lowerLim, double upperLim)>());
            _filtDic[feature].Add(featlim);
        }
    }

}

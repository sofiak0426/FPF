using System.Collections.Generic;
using System;
using System.Linq;


namespace FPF
{

    public class ds_Filters
    {
        public int featNum = 16;
        public int featMeetingCritNumCutoff = 1;

        //A dictionary that stores whether each feature in the filter is specified by user or not
        //Key: Feature name in param file; Value: if the param is specified by the user
        private Dictionary<string, bool> _featureIsSetDic = new Dictionary<string, bool>{
            {"charge", false},
            {"mass", false},
            {"peptide length", false},
            {"average reporter ion intensity", false},
            {"intra-peptide euclidean distance", false},
            {"intra-protein euclidean distance", false},
            {"number of ptms", false},
            {"ptm ratio", false},
            {"absolute mass difference", false},
            {"absolute precursor m/z difference", false},
            {"dot product", false},
            {"deltad", false},
            {"number of hits", false},
            {"mean of dot products of the hits", false},
            {"standard deviation of dot products of the hits", false},
            {"f-value", false}
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

        /// <summary>
        /// Adds user-specified filters for each feature to filterObj for further use.
        /// </summary>
        /// <param name="feature"> Feature name</param>
        /// <param name="filterStr"> The string containing filters for a single filter, read from the param file, e.g. 0.6-1 </param>
        public void AddFilters(string feature, string filterStr)
        {
            //If the user did not specify filters of this feature
            if (filterStr.ToLower() == "none")
                return;

            String[] filterArr = filterStr.Split(',').Select(filter => filter.Trim()).ToArray();
            foreach (string filter in filterArr)
            {
                if (filter.IndexOf('-') == -1)
                    throw new ApplicationException(String.Format("Feature \"{0}\": wrong filter format \"{1}\"", feature, filterStr));

                String[] filtLimArr = filter.Split('-').Select(filtLim => filtLim.Trim()).ToArray(); //Containing strings for lower and upper limits of one filter
                (double lowerLim, double upperLim) filtLim;
                //Set up filter lower limit
                if (filtLimArr[0] == String.Empty)
                    filtLim.lowerLim = Double.NegativeInfinity;
                else if(double.TryParse(filtLimArr[0], out filtLim.lowerLim) == false)
                    throw new ApplicationException(String.Format("Feature {0}: wrong lower limit format", feature));
                //Set up filter upper limit
                if (filtLimArr[1] == String.Empty)
                    filtLim.upperLim = Double.PositiveInfinity;
                else if (double.TryParse(filtLimArr[1], out filtLim.upperLim) == false)
                    throw new ApplicationException(String.Format("Feature {0}: wrong upper limit format", feature));
                this.AddFilter(feature, filtLim);
            }
            return;
        }
    }

}

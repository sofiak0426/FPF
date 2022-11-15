using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace S2I_Filter
{
    public class ds_Parameters
    {
        public List<string> _rawDataLi = new List<string>();

        public string MainDir { get; set; } 
        public string DbIproFile { get; set; }
        public string DataType { get; set; }
        public double CenWinSize { get; set; }
        public double IsoWinSize { get; set; }
        public double PrecursorTol { get; set; }
        public double PrecurIsoTol { get; set; }
        public double S2IThresh { get; set; }
        public List<string> RawDataLi {
            get { return _rawDataLi; }
            set { _rawDataLi = value;} 
        }

        //A dictionary that stores whether the global param values is correctly specified by user or not
        //Key: Parameter name in param file; Value: if the param is correctly specified by the user
        private Dictionary<string, bool> _paramIsSetDic = new Dictionary<string, bool>{
            {"main directory", false},
            {"iprophet search file", false},
            {"datatype", false},
            {"centroid window size", false},
            {"isolation window size", false},
            {"precursor m/z tolerance", false},
            {"precursor isotopic peak m/z tolerance", false},
            {"s2i threshold", false}
        };

        /// <summary>
        /// Check whether the current param name in the param file corresponds to one of the the correct param names in the dictionary.
        /// True: param name is correct; False: there is no corresponding param name
        /// </summary>
        public bool ValidateParamName(string paramNameInParamFile)
        {
            return _paramIsSetDic.ContainsKey(paramNameInParamFile);
        }

        /// <summary>
        /// If a param is correctly specified by user, change the item value (whose key is the parameter name) in _paramIsSetDic to true
        /// </summary>
        public void SetParamAsTrue(string paramName)
        {
            _paramIsSetDic[paramName] = true;
        }

        /// <summary>
        /// Check if a param is correctly specified by the user or not
        /// </summary>
        /// <returns></returns>
        public bool GetParamIsSet(string paramName)
        {
            return _paramIsSetDic[paramName];
        }

        /// <summary>
        /// Check whether all params in _paramIsSetDic are correctly specified by the user.
        /// Then return a list containing all parameters names that are not specified. 
        /// If all params are correctly specified, an empty list will by returned.
        /// </summary>
        public List<string> CheckAllParamsSet()
        {
            List<string> missingParams = new List<string>();
            foreach (KeyValuePair<string, bool> feature_hasValue in _paramIsSetDic)
            {
                if (feature_hasValue.Value == false)
                    missingParams.Add(feature_hasValue.Key);
            }
            return missingParams;
        }
    }
}

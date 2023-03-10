using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;

namespace FPF
{
    public class ds_Parameters
    {
        private string _mainDir; //Main directory of all the files related to FPF (except for the parameter file)
        private string _dbIproFile; //Name of the iProphet file from identification based on DB searching
        private string _dbslIproFile; //Name of the iProphet file from identification based on DB+SL searching
        private string _modDbslIproFile; //Name of the modified iProphet file from identification based on DB+SL searching
        private int _channelCnt;//Total number of channels
        private int _refChannel;//Number of reference channel (starting from 1)
        private List<(string, string)> _decoyKeywordLi = new List<(string, string)>(); //Array storing prefixes of decoy proteins
        private float _dbFdr001Prob; // FDR 1% probability of identification based on DB searching
        private float _dbslFdr001Prob; //FDR 1% probability of identification based on DB+SL searching

        //A dictionary that stores whether the global param values are correctly specified by the user or not
        //Key: Parameter names in param file; Value: if the param is correctly specified by the user
        private Dictionary<string, bool> _paramIsSetDic = new Dictionary<string, bool>{
            {"main directory",false},
            {"iprophet file from identification based on db searching",false},
            {"iprophet file from identification based on db+sl searching",false},
            {"output iprophet file",false},
            {"output csv file",false},
            {"reference channel",false},
            {"decoy prefixes or suffixes",false},
            {"proteins to be excluded from normalization",false}
        };

        public string MainDir
        {
            get { return _mainDir; }
            set { _mainDir = value; }
        }

        public string DbIproFile
        {
            get { return _dbIproFile; }
            set { _dbIproFile = value; }
        }
        public string DbslIproFile
        {
            get { return _dbslIproFile; }
            set { _dbslIproFile = value; }
        }
        public string ModDbslIproFile
        {
            get { return _modDbslIproFile; }
            set { _modDbslIproFile = value; }
        }
        public int ChannelCnt
        {
            get { return this._channelCnt; }
            set
            {
                this._channelCnt = value;
                if (this._refChannel > this._channelCnt)
                    throw new ApplicationException("Reference channel is out of bounds of total channels!");
            }
        }
        public int RefChannel
        {
            get { return _refChannel; }
            set { _refChannel = value; }
        }
        public List<(string, string)> DecoyKeywordLi
        {
            get { return _decoyKeywordLi;}
        }
        public float DbFdr001Prob
        {
            get { return _dbFdr001Prob; }
            set { _dbFdr001Prob = value; }
        }
        public float DbslFdr001Prob
        {
            get { return _dbslFdr001Prob; }
            set { _dbslFdr001Prob = value; }
        }

        /// <summary>
        /// Check whether the current param name in the param file corresponds to one of the correct param names in the dictionary.
        /// True: param name is correct; False: there is no corresponding param name
        /// </summary>
        public bool ValidateParamName (string paramNameInParamFile)
        {
            return _paramIsSetDic.ContainsKey(paramNameInParamFile);
        }

        /// <summary>
        /// If a param is correctly specified by user, change the item value (whose key is the parameter name) in _paramIsSetDic to true
        /// </summary>
        public void SetParamAsTrue (string paramName)
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
        /// Then return a list containing all parameter names that are not specified. 
        /// If all params are correctly specified, the function returns an empty list.
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

        /// <summary>
        /// Add decoy protein prefixes or suffixes to _decoyKeywordLi
        /// </summary>
        /// <param name="decoyKeywordArr"></param>
        public void AddDecoyKeyword(string[] decoyKeywordArr)
        {
            foreach (string str in decoyKeywordArr)
            {
                if (str.EndsWith('-') && !str.StartsWith('-'))
                    this._decoyKeywordLi.Add(("PRE", str.Substring(0, str.Length - 1)));
                else if (str.StartsWith('-') && !str.EndsWith('-')) 
                    this._decoyKeywordLi.Add(("SUF", str.Substring(1)));
                else
                    throw new ApplicationException(string.Format("Error: decoy-protein keywords are specified in the wrong format: {0}", (object)str));
            }
        }
    }
}

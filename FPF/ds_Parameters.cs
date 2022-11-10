using System.Collections.Generic;
using System;

namespace FPF
{
    public class ds_Parameters
    {
        private string _mainDir; //Main directory of all the files related to FPF (except for the parameter file)
        private string _iproDbFile; //File name for DB iprophet search result
        private string _iproDbSpstFile; //File name for DB + SL iprophet search result
        private string _modIproDbSpstFile; //Output name (the filtered DB + SL iprophet file)
        private int _channelCnt;//Total number of channels
        private int _refChannel;//Number of reference channel (starting from 1)
        private List<(string, string)> _decoyKeywordLi = new List<(string, string)>(); //Array storing prefixes of decoy proteins
        private float _dbFdr001Prob; // FDR 1% probability of DB iprophet search
        private float _dbSpstFdr001Prob; //FDR 1% probability of DB + SL iprophet search

        //A dictionary that stores whether the global param values is correctly specified by user or not
        //Key: Parameter name in param file; Value: if the param is correctly specified by the user
        private Dictionary<string, bool> _paramIsSetDic = new Dictionary<string, bool>{
            {"main directory", false},
            {"database iprophet search file", false},
            {"database + spectrast iprophet search file", false},
            {"output iprophet file", false},
            {"output csv file for filtered-out psms", false},
            {"reference channel", false},
            {"decoy prefixes or suffixes", false},
            {"proteins to be excluded from normalization", false}
        };

        public string MainDir
        {
            get { return _mainDir; }
            set { _mainDir = value; }
        }

        public string IproDbFile
        {
            get { return _iproDbFile; }
            set { _iproDbFile = value; }
        }
        public string IproDbSpstFile
        {
            get { return _iproDbSpstFile; }
            set { _iproDbSpstFile = value; }
        }
        public string ModIproDbSpstFile
        {
            get { return _modIproDbSpstFile; }
            set { _modIproDbSpstFile = value; }
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
        public float DbSpstFdr001Prob
        {
            get { return _dbSpstFdr001Prob; }
            set { _dbSpstFdr001Prob = value; }
        }

        /// <summary>
        /// Check whether the current param name in the param file corresponds to one of the the correct param names in the dictionary.
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
                    throw new ApplicationException(string.Format("Error: you specified decoy-protein keywords in the wrong format: {0}", (object)str));
            }
        }
    }
}

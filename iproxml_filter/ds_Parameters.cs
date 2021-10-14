using System.Collections.Generic;

namespace FPF
{
    public class ds_Parameters
    {
        private string _iproDbFile; //File name for DB iprophet search result
        private string _iproDbSpstFile; //File name for DB + SL iprophet search result
        private string _modIproDbSpstFile; //Output name (the filtered DB + SL iprophet file)
        private int _channelCnt;//Total number of channels
        private int _refChannel;//Number of reference channel (starting from 1)
        private string[] _decoyPrefixArr; //Array storing prefixes of decoy proteins
        private float _dbFdr001Prob; // FDR 1% probability of DB iprophet search
        private float _dbSpstFdr001Prob; //FDR 1% probability of DB + SL iprophet search

        //A dictionary that stores whether the global param values is correctly specified by user or not
        //Key: Parameter name in param file; Value: if the param is correctly specified by the user
        private Dictionary<string, bool> _paramIsSetDic = new Dictionary<string, bool>{
            {"Database Iprophet Search File", false},
            {"Database + SpectraST Iprophet Search File", false},
            {"Output Iprophet File", false},
            {"Output Csv File for Filtered-out PSMs", false},
            {"Number of Channels", false},
            {"Reference Channel", false},
            {"Decoy Prefix", false},
            {"Background Keywords for Normalization", false}
        };

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
            get { return _channelCnt; }
            set { _channelCnt = value; }
        }
        public int RefChannel
        {
            get { return _refChannel; }
            set { _refChannel = value; }
        }
        public string[] DecoyPrefixArr
        {
            get { return _decoyPrefixArr; }
            set { _decoyPrefixArr = value; }
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
    }
}

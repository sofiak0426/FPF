using System;
using System.Collections.Generic;
using System.Text;

namespace iproxml_filter
{
    public class ds_Parameters
    {
        private string _iproDbFile;
        private string _iproDbSpstFile;
        private string _modIproDbSpstFile; //Output file name for the whole project
        private int _channelCnt;
        private int _refChannel;
        private string _decoyPrefix;
        private float _dbFdr001Prob;
        private float _dbSpstFdr001Prob;

        // Stores that whether the global parameter values is specified by user or not
        private Dictionary<string, bool> _paramIsSetDic = new Dictionary<string, bool>{
            {"Database Iprophet Search File", false},
            {"Database + SpectraST Iprophet Search File", false},
            {"Output File", false},
            {"Channel Number", false},
            {"Reference Channel", false},
            {"Decoy Prefix", false},
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
        public string DecoyPrefix
        {
            get { return _decoyPrefix; }
            set { _decoyPrefix = value; }
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
        /// <param name="stringInParamFile"></param>
        /// <returns></returns>
        public bool ValidateParamDescription (string stringInParamFile)
        {
            return _paramIsSetDic.ContainsKey(stringInParamFile);
        }

        /// <summary>
        /// If one parameter is correctly specified by user, change the item value (whose key is the parameter name) to true
        /// </summary>
        /// <param name="paramName"></param>
        public void SetParamAsTrue (string paramName)
        {
            _paramIsSetDic[paramName] = true;
        }

        public bool GetParamIsSet(string paramName)
        {
            return _paramIsSetDic[paramName];
        }

        /// <summary>
        /// Check whether all parameters in the dic are correctly specified by the user.
        /// Then return a list containing all parameters names that are not specified.
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

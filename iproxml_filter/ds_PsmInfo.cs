using System;
using System.Collections.Generic;
using System.Text;

namespace iproxml_filter
{
    public class PsmInfo
    {
        private Dictionary<string, object> _featureValueDic = new Dictionary<string, object>();

        public PsmInfo() { }

        public PsmInfo(double mass, int charge,int peplen)
        {
            _featureValueDic.Add("Mass", mass);
            _featureValueDic.Add("Charge",charge);
            _featureValueDic.Add("Peptide Length", peplen);
        }

        public Dictionary<string, object> FeatureValueDic
        {
            get { return _featureValueDic; }
        }

        public object GetFeatureValue(string key)
        {
            return _featureValueDic[key];
        }

        public void SetFeatureValue(string key, object value)
        {
            if (ds_FilterParam.featAndTypeDic.ContainsKey(key))
                return;

            if (!_featureValueDic.ContainsKey(key))
                _featureValueDic.Add(key, value);
            else
                _featureValueDic[key] = value;
        }
    }
}

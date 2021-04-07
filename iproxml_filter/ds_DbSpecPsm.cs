using System.Collections.Generic;

namespace iproxml_filter
{
    public class ds_DbSpecPsm
    {
        private string _name;
        private List<double> _ratioLi;
        private double _intraPepEu;
        private double _intraProtEu;

        public ds_DbSpecPsm(string name, List<double> ratio)
        {
            this._name = name;
            this._ratioLi = ratio;
            this._intraPepEu = 0;
            this._intraProtEu = 0;
        }

        public string Name
        {
            get { return this._name; }
        }

        public List<double> ErrorLi
        {
            get { return this._ratioLi; }
        }

        public double IntraPepEu
        {
            get { return this._intraPepEu; }
            set { _intraPepEu = value; }
        }

        public double IntraProtEu
        {
            get { return this._intraProtEu; }
            set { _intraProtEu = value; }
        }
    }
}

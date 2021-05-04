using System;
using System.Collections.Generic;
using System.Text;

namespace iproxml_filter
{
    public class PsmInfo
    {
        private double _mass;
        private int _charge;
        private int _peplen;
        private double _avgInten;
        private double _intraPepEuDist;
        private double _intraProtEuDist;

        public PsmInfo() { }

        public PsmInfo(double mass, int charge,int peplen)
        {
            this._mass = mass;
            this._charge = charge;
            this._peplen = peplen;
        }

        public double Mass
        {
            get { return _mass; }
            set { _mass = value; }
        }
        public int Charge
        {
            get { return _charge; }
            set { _charge = value; }
        }

        public int Peplen
        {
            get { return _peplen; }
            set { _peplen = value; }
        }
 
        public double AvgInten
        {
            get { return _avgInten; }
            set { _avgInten = value; }
        }

        public double IntraPepEuDist
        {
            get { return _intraPepEuDist; }
            set { _intraPepEuDist = value; }
        }

        public double IntraProtEuDist
        {
            get { return _intraProtEuDist; }
            set { _intraProtEuDist = value; }
        }

    }
}

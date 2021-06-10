namespace iproxml_filter
{
    public class ds_Psm_ForFilter
    {
        private int _charge;
        private double _mass;
        private int _peplen;
        private double _avgInten;
        private double _intraPepEuDist;
        private double _intraProtEuDist;
        private int _ptmCount;
        private double _ptmRatio;
        private double _absMassDiff;
        private double _absPrecursorMzDiff;
        private double _dotProduct;
        private double _deltaScore;
        private double _hitsNum;
        private double _hitsMean;
        private double _hitsStdev;
        private double _fVal;

        public ds_Psm_ForFilter() { }

        public ds_Psm_ForFilter(double mass, int charge, int peplen, int ptmCount, double ptmRatio, double absMassDiff, 
            double absPrecursorMzDiff, double dotProduct, double deltaScore, double hitsNum, double hitsMean, double hitsStdev, double fVal)
        {
            this._mass = mass;
            this._charge = charge;
            this._peplen = peplen;
            this._ptmCount = ptmCount;
            this._ptmRatio = ptmRatio;
            this._absMassDiff = absMassDiff;
            this._absPrecursorMzDiff = absPrecursorMzDiff;
            this._dotProduct = dotProduct;
            this._deltaScore = deltaScore;
            this._hitsNum = hitsNum;
            this._hitsMean = hitsMean;
            this._hitsStdev = hitsStdev;
            this._fVal = fVal;
        }

        public int Charge
        {
            get { return _charge; }
            set { _charge = value; }
        }
        public double Mass
        {
            get { return _mass; }
            set { _mass = value; }
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
        public int PtmCount
        {
            get { return _ptmCount; }
            set { _ptmCount = value; }
        }
        public double PtmRatio
        {
            get { return _ptmRatio; }
            set { _ptmRatio = value; }
        }
        public double AbsMassDiff
        {
            get { return _absMassDiff; }
            set { _absMassDiff = value; }
        }
        public double AbsPrecursorMzDiff
        {
            get { return _absPrecursorMzDiff; }
            set { _absPrecursorMzDiff = value; }
        }
        public double DotProduct
        {
            get { return _dotProduct; }
            set { _dotProduct = value; }
        }
        public double DeltaScore
        {
            get { return _deltaScore; }
            set { _deltaScore = value; }
        }
        public double HitsNum
        {
            get { return _hitsNum; }
            set { _hitsNum = value; }
        }
        public double HitsMean
        {
            get { return _hitsMean; }
            set { _hitsMean = value; }
        }
        public double HitsStdev
        {
            get { return _hitsStdev; }
            set { _hitsStdev = value; }
        }
        public double Fval
        {
            get { return _fVal; }
            set { _fVal = value; }
        }

    }
}

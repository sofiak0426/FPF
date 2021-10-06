namespace FPF
{
    public class ds_Psm_ForFilter
    {
        private string _protID; 
        private int _charge;
        private double _mass;
        private int _peplen;
        private double _avgInten = -1000; //Average reporter ion intensity of the PSM. If the PSM contains zero repoter ion intensities, set the value to -1. (This feature will not be considered then.)
        private double _intraPepEuDist = -1000; //Intra-peptide euclidean distance of the PSM. If the PSM contains zero repoter ion intensities, set the value to -1. (This feature will not be considered then.)
        private double _intraProtEuDist = -1000; //Intra-protein euclidean distance of the PSM. If the PSM contains zero repoter ion intensities, set the value to -1. (This feature will not be considered then.)
        private int _ptmCount;
        private double _ptmRatio;
        private double _absMassDiff; //Absolute mass difference
        private double _absPrecursorMzDiff; //Absolute precursor m/z difference
        private double _dotProduct;
        private double _deltaScore;
        private double _hitsNum;
        private double _hitsMean;
        private double _hitsStdev;
        private double _fVal; //F-value

        public ds_Psm_ForFilter() { }

        public ds_Psm_ForFilter(string protID, double mass, int charge, int peplen, int ptmCount, double ptmRatio, double absMassDiff, 
            double absPrecursorMzDiff, double dotProduct, double deltaScore, double hitsNum, double hitsMean, double hitsStdev, double fVal)
        {
            this._protID = protID;
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

        public string ProtID
        {
            get { return _protID; }
            set { _protID = value; }
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

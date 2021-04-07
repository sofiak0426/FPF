using System;
using System.Collections.Generic;

namespace iproxml_filter
{
    public class ds_PsmFilterParam //list for selected ranges for each feature
    {
        private List<(double lowerLim, double upperLim)> _chargeFiltLi = new List<(double lowerLim, double upperLim)>();
        private List<(double lowerLim, double upperLim)> _massFiltLi = new List<(double lowerLim, double upperLim)>();
        private List<(double lowerLim, double upperLim)> _pepLenFiltLi = new List<(double lowerLim, double upperLim)>();
        private List<(double lowerLim, double upperLim)> _intraPepEuFiltLi = new List<(double lowerLim, double upperLim)>();
        private List<(double lowerLim, double upperLim)> _intraProtEuFiltLi = new List<(double lowerLim, double upperLim)>();

        public List<(double lowerLim, double upperLim)> ChargeFiltLi
        {
            get { return _chargeFiltLi; }
        }

        public List<(double lowerLim, double upperLim)> MassFiltLi
        {
            get { return _massFiltLi; }
        }

        public List<(double lowerLim, double upperLim)> PepLenFiltLi
        {
            get { return _pepLenFiltLi; }
        }

        public List<(double lowerLim, double upperLim)> IntraPepEuFiltLi
        {
            get { return _intraPepEuFiltLi; }
        }

        public List<(double lowerLim, double upperLim)> IntraProtEuFiltLi
        {
            get { return _intraProtEuFiltLi; }
        }

        public bool AddFilter(string filtType, (double lowerLim, double upperLim) featlim)
        {
            switch(filtType)
            {
                case "Charge":
                    this._chargeFiltLi.Add(featlim);
                    break;
                case "Mass":
                    this._massFiltLi.Add(featlim);
                    break;
                case "Peptide Length":
                    this._pepLenFiltLi.Add(featlim);
                    break;
                case "Intra-Peptide Euclidean Distance":
                    this._intraPepEuFiltLi.Add(featlim);
                    break;
                case "Intra-Protein Euclidean Distance":
                    this._intraProtEuFiltLi.Add(featlim);
                    break;
                default:
                    return false;
            }
            return true;
        }

        public void PrintFilter() //for testing
        {
            foreach ((double lowerLim, double upperLim) featLim in this.IntraPepEuFiltLi)
                Console.WriteLine(featLim.lowerLim.ToString() + featLim.upperLim.ToString());
        }
    }

}

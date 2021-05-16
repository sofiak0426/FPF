using System;

namespace iproxml_filter
{
    class Program
    {
        static void Main(string[] args)
        {
           FPFActions startAction = new FPFActions();
           startAction.MainActions(args[0], args[1]); //0: main dir; 1: param file
        }
    }
}

using System;

namespace FPF
{
    class Program
    {
        static void Main(string[] args)
        {
           FPFActions startAction = new FPFActions();
           startAction.MainActions(args[0]); //args[0]: parameter file name
        }
    }
}

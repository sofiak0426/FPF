using System;

namespace FPF
{
    class Program
    {
        static void Main(string[] args)
        {
           FPFActions startAction = new FPFActions();
           startAction.MainActions(args[0], args[1]); //args[0]: main dir; args[1]: param file
        }
    }
}

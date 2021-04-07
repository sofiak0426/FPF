using System;

namespace iproxml_filter
{
    class Program
    {
        static void Main(string[] args)
        {
           Actions startAction = new Actions();
           startAction.MainActions(args[0], args[1]);
        }
    }
}

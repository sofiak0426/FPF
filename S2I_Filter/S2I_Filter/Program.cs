namespace S2I_Filter
{
    class Program
    {
        public static void Main(string[] args)
        {
            S2I_FilterActions startAction = new S2I_FilterActions();
            startAction.MainActions(args[0]); //args[0]: the param file name
        }
    }
}

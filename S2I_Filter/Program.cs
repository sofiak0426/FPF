namespace S2I_Filter
{
    class Program
    {
        public static void Main(string[] args)
        {
            S2I_FilterActions startAction = new S2I_FilterActions();
            startAction.MainActions(args); //args[0]: dataDir; args[1]: param file
        }
    }
}

namespace VST.NET
{
   public class VstProgram
   {
      public readonly VstLibraryContext Library;
      public readonly int Index;
      public readonly string Name;

      public VstProgram(VstLibraryContext library, int index)
      {
         Library = library;
         Index = index;
         Name = library.GetProgramName(index);
      }

      public void Activate()
      {
         Library.SetProgram(Index);
      }
   }
}

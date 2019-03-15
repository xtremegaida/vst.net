namespace VST.NET
{
   public class VstParameter
   {
      public readonly VstLibraryContext Library;
      public readonly int Index;
      public readonly string Name;
      public readonly string Label;
      public readonly string Display;
      public readonly float DefaultValue;

      public VstParameter(VstLibraryContext library, int index)
      {
         Library = library;
         Index = index;
         Name = library.GetParameterName(index);
         Label = library.GetParameterLabel(index);
         Display = library.GetParameterDisplay(index);
         DefaultValue = library.GetParameter(index);
      }

      public float Get()
      {
         return (Library.GetParameter(Index));
      }

      public void Set(float value)
      {
         Library.SetParameter(Index, value);
      }
   }
}

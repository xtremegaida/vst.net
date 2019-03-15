using System;

namespace VST.NET
{
   [Flags]
   public enum VstMidiEventFlags : int
   {
      IsRealtime = 1 << 0
   }
}

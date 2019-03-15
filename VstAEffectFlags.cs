using System;

namespace VST.NET
{
   [Flags]
   internal enum VstAEffectFlags : int
   {
      HasEditor = 1 << 0,
      CanReplacing = 1 << 4,
      ProgramChunks = 1 << 5,
      IsSynth = 1 << 8,
      NoSoundInStop = 1 << 9,
      CanDoubleReplacing = 1 << 12,
      
      // deprecated:
      HasClip = 1 << 1,
      HasVu = 1 << 2,
      HasCanMono = 1 << 3,
      HasExtIsAsync = 1 << 10,
      HasExtHasBuffer = 1 << 11,
   }
}

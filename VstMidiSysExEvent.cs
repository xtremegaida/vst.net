using System;
using System.Runtime.InteropServices;

namespace VST.NET
{
   public struct VstMidiSysExEvent
   {
      private static readonly int byteSize = 20 + (Marshal.SizeOf(typeof(IntPtr)) * 3);

      public int DeltaFrames;
      public int Flags;
      public byte[] SysExData;

      public VstEvent ToEvent()
      {
         return (new VstEvent()
         {
            Type = VstEventType.Midi,
            ByteSize = byteSize,
            SampleIndex = this.DeltaFrames,
            Flags = this.Flags,
            SysExData = this.SysExData
         });
      }

      public static implicit operator VstEvent(VstMidiSysExEvent midi) { return (midi.ToEvent()); }
   }
}

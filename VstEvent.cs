namespace VST.NET
{
   public struct VstEvent
   {
      public VstEventType Type;
      public int ByteSize; // Size excluding Type + ByteSize for generic events
      public int SampleIndex;
      public int Flags;
      public int Data_0_3;
      public int Data_4_7;
      public int Data_8_11;
      public int Data_12_15;
      public byte[] SysExData;

      public byte this[int index]
      {
         get
         {
            int shift = (index & 3) << 3; index >>= 2;
            if (index == 0) { return ((byte)((Data_0_3 >> shift) & 0xff)); }
            if (index == 1) { return ((byte)((Data_4_7 >> shift) & 0xff)); }
            if (index == 2) { return ((byte)((Data_8_11 >> shift) & 0xff)); }
            if (index == 3) { return ((byte)((Data_12_15 >> shift) & 0xff)); }
            return (0);
         }
         set
         {
            int shift = (index & 3) << 3; index >>= 2;
            if (index == 0) { Data_0_3 &= ~(0xff << shift); Data_0_3 |= (int)value << shift; return; }
            if (index == 1) { Data_4_7 &= ~(0xff << shift); Data_4_7 |= (int)value << shift; return; }
            if (index == 2) { Data_8_11 &= ~(0xff << shift); Data_8_11 |= (int)value << shift; return; }
            if (index == 3) { Data_12_15 &= ~(0xff << shift); Data_12_15 |= (int)value << shift; return; }
         }
      }
   }
}

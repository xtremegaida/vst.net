namespace VST.NET
{
   public struct VstMidiEvent
   {
      public int SampleIndex;
      public VstMidiEventFlags Flags;
      public int NoteLength;
      public int NoteOffset;
      public byte MidiCommand;
      public byte MidiData0;
      public byte MidiData1;
      public byte Detune;
      public byte NoteOffVelocity;

      public VstEvent ToEvent()
      {
         return (new VstEvent()
         {
            Type = VstEventType.Midi,
            ByteSize = 32,
            SampleIndex = this.SampleIndex,
            Flags = (int)this.Flags,
            Data_0_3 = NoteLength,
            Data_4_7 = NoteOffset,
            Data_8_11 = (int)MidiCommand | ((int)MidiData0 << 8) | ((int)MidiData1 << 16),
            Data_12_15 = (int)Detune | ((int)NoteOffVelocity << 8)
         });
      }

      public static implicit operator VstEvent(VstMidiEvent midi) { return (midi.ToEvent()); }
   }
}

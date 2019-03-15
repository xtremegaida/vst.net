namespace VST.NET
{
   public enum VstEventType : int
   {
      Unknown = 0,
      Midi = 1,
      Audio = 2,     // deprecated
      Video = 3,     // deprecated
      Parameter = 4, // deprecated
      Trigger = 5,   // deprecated
      MidiSysEx = 6
   }
}

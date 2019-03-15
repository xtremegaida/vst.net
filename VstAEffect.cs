using System;
using System.Runtime.InteropServices;

namespace VST.NET
{
   [StructLayout(LayoutKind.Sequential)]
   internal struct VstAEffect
   {
      public const int CheckMatch = ((int)'V' << 24) + ((int)'s' << 16) + ((int)'t' << 8) + ((int)'P');

      public int Check;         // == checkMatch;
      public IntPtr Dispatcher;
      public IntPtr Process;    // deprecated
      public IntPtr SetParameter;
      public IntPtr GetParameter;
      public int ProgramCount;
      public int ParameterCount;
      public int InputCount;
      public int OutputCount;
      public VstAEffectFlags Flags;
      public IntPtr Reserved1;
      public IntPtr Reserved2;
      public int InitialDelay;
      public int RealQualities; // deprecated; unused
      public int OffQualities;  // deprecated; unused
      public int IoRatio;       // deprecated; unused
      public IntPtr Object;
      public IntPtr User;
      public int UniqueID;
      public int Version;
      public IntPtr ProcessReplacing;
   }

   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   internal delegate IntPtr VstDispatcherCallback(IntPtr effectHandle, int opcode, int index, IntPtr value, IntPtr data, float opt);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   internal delegate void VstProcessCallback(IntPtr effectHandle, IntPtr inputs, IntPtr outputs, int blockSize);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   internal delegate void VstSetParameterProcCallback(IntPtr effectHandle, int index, float parameter);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   internal delegate float VstGetParameterProcCallback(IntPtr effectHandle, int index);
}

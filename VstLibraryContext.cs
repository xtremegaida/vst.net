using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace VST.NET
{
   public class VstLibraryContext : IDisposable
   {
      [DllImport("Kernel32.dll")]
      private static extern IntPtr LoadLibrary(string path);

      [DllImport("Kernel32.dll")]
      private static extern void FreeLibrary(IntPtr hModule);

      [DllImport("Kernel32.dll")]
      private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

      [DllImport("kernel32.dll")]
      private static extern void RtlZeroMemory(IntPtr dst, int length);

      [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
      private delegate IntPtr VstMainCallback(IntPtr callback);

      private readonly static VstDispatcherCallback hostDispatcherCallback = new VstDispatcherCallback(HostDispatcherCallback);
      private readonly static IntPtr hostDispatcherHandle = Marshal.GetFunctionPointerForDelegate(hostDispatcherCallback);

      private const int bufferBytes = 256;

      private static readonly int ptrBytes = Marshal.SizeOf(typeof(IntPtr));
      private static readonly VstEvent[] emptyEvents = new VstEvent[0];

      internal IntPtr LibraryHandle;
      internal IntPtr EffectHandle;
      internal IntPtr Buffer;
      internal readonly VstDispatcherCallback DispatcherDelegate;
      internal readonly VstProcessCallback ProcessReplacingDelegate;
      internal readonly VstGetParameterProcCallback GetParameterDelegate;
      internal readonly VstSetParameterProcCallback SetParameterDelegate;
      internal readonly VstAEffect Effect;

      private bool isOpen;
      private bool isOn;
      private bool isProcessing;
      private bool isEditorOpen;
      private bool eventsProcessed;

      private IntPtr eventsBuffer;
      private int eventsBufferSize;

      private IntPtr audioBuffer;
      private int audioOutputArrayOffset;
      private int audioInputOffset;
      private int audioOutputOffset;
      private int audioBufferSize;
      private int blockSize = 1024;
      private int blockBytes;
      private float[] mixBuffer;

      public readonly int PluginID;
      public readonly int PluginVersion;
      public readonly string FileName;
      public readonly string EffectName;
      public readonly VstProgram[] Programs;
      public readonly VstParameter[] Parameters;

      public readonly int InputCount;
      public readonly int OutputCount;

      public readonly bool CanProcessReplacing;
      public readonly bool CanProcessEvents;

      #region Constructors

      public VstLibraryContext(string fileName)
      {
         LibraryHandle = LoadLibrary(fileName);
         if (LibraryHandle == IntPtr.Zero) { throw new Exception("Failed to load library - file not found or not a valid library."); }
         try
         {
            IntPtr func = GetProcAddress(LibraryHandle, "VSTPluginMain");
            if (func == IntPtr.Zero) { func = GetProcAddress(LibraryHandle, "main"); }
            if (func == IntPtr.Zero) { throw new Exception("Not a VST plugin: VSTPluginMain not found."); }

            VstMainCallback instantiate = (VstMainCallback)Marshal.GetDelegateForFunctionPointer(func, typeof(VstMainCallback));
            EffectHandle = instantiate(hostDispatcherHandle);
            if (EffectHandle == IntPtr.Zero) { throw new Exception("VSTPluginMain returned a null pointer."); }
            Effect = (VstAEffect)Marshal.PtrToStructure(EffectHandle, typeof(VstAEffect));

            if (Effect.Check != VstAEffect.CheckMatch) { throw new Exception("Error loading plugin: 'VstP' checksum mismatch."); }
            if (Effect.Dispatcher != null) { DispatcherDelegate = (VstDispatcherCallback)Marshal.GetDelegateForFunctionPointer(Effect.Dispatcher, typeof(VstDispatcherCallback)); }
            if (Effect.ProcessReplacing != null) { ProcessReplacingDelegate = (VstProcessCallback)Marshal.GetDelegateForFunctionPointer(Effect.ProcessReplacing, typeof(VstProcessCallback)); }
            if (Effect.GetParameter != null) { GetParameterDelegate = (VstGetParameterProcCallback)Marshal.GetDelegateForFunctionPointer(Effect.GetParameter, typeof(VstGetParameterProcCallback)); }
            if (Effect.SetParameter != null) { SetParameterDelegate = (VstSetParameterProcCallback)Marshal.GetDelegateForFunctionPointer(Effect.SetParameter, typeof(VstSetParameterProcCallback)); }

            Open();
            Buffer = Marshal.AllocHGlobal(bufferBytes);
            FileName = Path.GetFullPath(fileName);
            EffectName = GetEffectName();
            Programs = new VstProgram[Effect.ProgramCount];
            for (int i = 0; i < Programs.Length; i++) { Programs[i] = new VstProgram(this, i); }
            Parameters = new VstParameter[Effect.ParameterCount];
            for (int i = 0; i < Parameters.Length; i++) { Parameters[i] = new VstParameter(this, i); }

            PluginID = Effect.UniqueID;
            PluginVersion = Effect.Version;
            InputCount = Effect.InputCount;
            OutputCount = Effect.OutputCount;

            CanProcessReplacing = (ProcessReplacingDelegate != null && Effect.Flags.HasFlag(VstAEffectFlags.CanReplacing));
            CanProcessEvents = CanDo("receiveVstEvents") || CanDo("receiveVstMidiEvent");
         }
         catch
         {
            FreeLibrary(LibraryHandle);
            if (Buffer != IntPtr.Zero) { Marshal.FreeHGlobal(Buffer); }
            throw;
         }
      }

      #endregion

      #region Dispatcher Opcodes
      
      internal const int effOpen = 0;
      internal const int effClose = 1;
      internal const int effGetProgram = 2;
      internal const int effSetProgram = 3;
      internal const int effGetParamLabel = 6;
      internal const int effGetParamDisplay = 7;
      internal const int effGetParamName = 8;
      internal const int effSetSampleRate = 10;
      internal const int effSetBlockSize = 11;
      internal const int effMainsChanged = 12;
      internal const int effEditGetRect = 13;
      internal const int effEditOpen = 14;
      internal const int effEditClose = 15;
      internal const int effGetChunk = 23;
      internal const int effSetChunk = 24;
      internal const int effProcessEvents = 25;
      internal const int effGetProgramNameIndexed = 29;
      internal const int effGetEffectName = 45;
      internal const int effCanDo = 51;
      internal const int effStartProcess = 71;
      internal const int effStopProcess = 72;

      #endregion

      #region Callback

      private static IntPtr HostDispatcherCallback(IntPtr effectHandle, int opcode, int index, IntPtr value, IntPtr data, float opt)
      {
         if (opcode == 1) { return (new IntPtr(2400)); }
         return (IntPtr.Zero);
      }

      #endregion

      #region Internal

      internal string GetParameterLabel(int index)
      {
         if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return (null); }
         Marshal.WriteInt64(Buffer, 0);
         DispatcherDelegate(EffectHandle, effGetParamLabel, index, IntPtr.Zero, Buffer, 0);
         return (Marshal.PtrToStringAnsi(Buffer));
      }

      internal string GetParameterDisplay(int index)
      {
         if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return (null); }
         Marshal.WriteInt64(Buffer, 0);
         DispatcherDelegate(EffectHandle, effGetParamDisplay, index, IntPtr.Zero, Buffer, 0);
         return (Marshal.PtrToStringAnsi(Buffer));
      }

      internal string GetParameterName(int index)
      {
         if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return (null); }
         Marshal.WriteInt64(Buffer, 0);
         DispatcherDelegate(EffectHandle, effGetParamName, index, IntPtr.Zero, Buffer, 0);
         return (Marshal.PtrToStringAnsi(Buffer));
      }
      
      internal string GetProgramName(int index)
      {
         if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return (null); }
         Marshal.WriteInt64(Buffer, 0);
         DispatcherDelegate(EffectHandle, effGetProgramNameIndexed, index, IntPtr.Zero, Buffer, 0);
         return (Marshal.PtrToStringAnsi(Buffer));
      }

      internal string GetEffectName()
      {
         if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return (null); }
         Marshal.WriteInt64(Buffer, 0);
         DispatcherDelegate(EffectHandle, effGetEffectName, 0, IntPtr.Zero, Buffer, 0);
         return (Marshal.PtrToStringAnsi(Buffer));
      }

      #endregion

      #region Public

      public void Open()
      {
         lock (this)
         {
            if (isOpen) { return; }
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            DispatcherDelegate(EffectHandle, effOpen, 0, IntPtr.Zero, IntPtr.Zero, 0);
            isOpen = true;
         }
      }

      public void Close()
      {
         lock (this)
         {
            if (!isOpen) { return; }
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            DispatcherDelegate(EffectHandle, effClose, 0, IntPtr.Zero, IntPtr.Zero, 0);
            isOpen = false;
         }
      }

      public int GetProgram()
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return (0); }
            return (DispatcherDelegate(EffectHandle, effGetProgram, 0, IntPtr.Zero, IntPtr.Zero, 0).ToInt32());
         }
      }

      public void SetProgram(int index)
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            DispatcherDelegate(EffectHandle, effSetProgram, 0, new IntPtr(index), IntPtr.Zero, 0);
         }
      }

      public float GetParameter(int index)
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || GetParameterDelegate == null) { return (0); }
            return (GetParameterDelegate(EffectHandle, index));
         }
      }

      public void SetParameter(int index, float value)
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || SetParameterDelegate == null) { return; }
            SetParameterDelegate(EffectHandle, index, value);
         }
      }

      public void SetSampleRate(float value)
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            DispatcherDelegate(EffectHandle, effSetSampleRate, 0, IntPtr.Zero, IntPtr.Zero, value);
         }
      }

      public void SetBlockSize(int size)
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            if (size < 1) { size = 1; }
            if (blockSize != size)
            {
               if (audioBuffer != IntPtr.Zero)
               {
                  Marshal.FreeHGlobal(audioBuffer);
                  audioBuffer = IntPtr.Zero;
               }
               blockSize = size;
            }
            DispatcherDelegate(EffectHandle, effSetBlockSize, 0, new IntPtr(size), IntPtr.Zero, 0);
         }
      }

      public void On()
      {
         lock (this)
         {
            if (isOn) { return; }
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            DispatcherDelegate(EffectHandle, effMainsChanged, 0, new IntPtr(1), IntPtr.Zero, 0);
            isOn = true;
         }
      }

      public void Off()
      {
         lock (this)
         {
            if (!isOn) { return; }
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            if (isProcessing) { StopProcess(); }
            DispatcherDelegate(EffectHandle, effMainsChanged, 0, IntPtr.Zero, IntPtr.Zero, 0);
            isOn = false;
         }
      }

      public VstRectangle EditorGetRect()
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return (new VstRectangle()); }
            Marshal.WriteIntPtr(Buffer, IntPtr.Zero);
            DispatcherDelegate(EffectHandle, effEditGetRect, 0, IntPtr.Zero, Buffer, 0);
            IntPtr rectPtr = Marshal.ReadIntPtr(Buffer);
            if (rectPtr == IntPtr.Zero) { return (new VstRectangle()); }
            return ((VstRectangle)Marshal.PtrToStructure(rectPtr, typeof(VstRectangle)));
         }
      }

      public bool EditorOpen(IntPtr hWnd)
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return (false); }
            if (isEditorOpen) { EditorClose(); }
            return (isEditorOpen = (DispatcherDelegate(EffectHandle, effEditOpen, 0, IntPtr.Zero, hWnd, 0) != IntPtr.Zero));
         }
      }

      public void EditorClose()
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            DispatcherDelegate(EffectHandle, effEditClose, 0, IntPtr.Zero, IntPtr.Zero, 0);
         }
      }

      public byte[] GetChunk(bool currentProgramOnly = true)
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return (null); }
            Marshal.WriteInt64(Buffer, 0);
            int length = DispatcherDelegate(EffectHandle, effGetChunk, currentProgramOnly ? 1 : 0, IntPtr.Zero, Buffer, 0).ToInt32();
            if (length == 0) { return (null); }
            IntPtr data = Marshal.ReadIntPtr(Buffer);
            if (data == IntPtr.Zero) { return (null); }
            byte[] array = new byte[length];
            Marshal.Copy(data, array, 0, length);
            return (array);
         }
      }

      public void SetChunk(byte[] chunk, bool currentProgramOnly = true)
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null || chunk == null) { return; }
            IntPtr tmpBuffer = chunk.Length <= bufferBytes ? Buffer : Marshal.AllocHGlobal(chunk.Length);
            try
            {
               Marshal.Copy(chunk, 0, tmpBuffer, chunk.Length);
               DispatcherDelegate(EffectHandle, effSetChunk, currentProgramOnly ? 1 : 0, new IntPtr(chunk.Length), tmpBuffer, 0);
            }
            finally
            {
               if (tmpBuffer != Buffer) { Marshal.FreeHGlobal(tmpBuffer); }
            }
         }
      }

      public bool CanDo(string feature)
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null || feature == null) { return (false); }
            IntPtr stringBuffer = Marshal.StringToHGlobalAnsi(feature);
            try
            {
               return (DispatcherDelegate(EffectHandle, effCanDo, 0, IntPtr.Zero, stringBuffer, 0).ToInt32() > 0);
            }
            finally
            {
               Marshal.FreeHGlobal(stringBuffer);
            }
         }
      }

      public bool CanDo(VstCanDoFeature feature)
      {
         string str = feature.ToString() ?? string.Empty;
         if (str.Length == 0) { return (false); }
         if (str[0] == 'x') { str = str.Substring(1); }
         else { str = Char.ToLower(str[0]) + str.Substring(1); }
         return (CanDo(str));
      }

      public void StartProcess()
      {
         lock (this)
         {
            if (isProcessing) { return; }
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            if (!isOn) { On(); }
            DispatcherDelegate(EffectHandle, effStartProcess, 0, IntPtr.Zero, IntPtr.Zero, 0);
            isProcessing = true;
         }
      }

      public void StopProcess()
      {
         lock (this)
         {
            if (!isProcessing) { return; }
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            DispatcherDelegate(EffectHandle, effStopProcess, 0, IntPtr.Zero, IntPtr.Zero, 0);
            isProcessing = false;
         }
      }

      public void ProcessEvents(VstEvent[] events)
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero || DispatcherDelegate == null) { return; }
            int i, s, sm, size;
            VstEvent e;

            if (events == null) { events = emptyEvents; }
            size = (ptrBytes + ptrBytes + ptrBytes + 4) + (ptrBytes * events.Length);
            for (i = 0; i < events.Length; i++)
            {
               e = events[i];
               switch (e.Type)
               {
                  case VstEventType.Midi: size += 32 + 8; break;
                  case VstEventType.MidiSysEx: size += 20 + 16 + (3 * ptrBytes) + (e.SysExData != null ? e.SysExData.Length : 0); break;
                  default: size += 8 + events[i].ByteSize + 7; break;
               }
            }

            if (eventsBuffer == IntPtr.Zero || size > eventsBufferSize)
            {
               if (eventsBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(eventsBuffer); }
               eventsBufferSize = size;
               eventsBuffer = Marshal.AllocHGlobal(eventsBufferSize);
            }

            Marshal.WriteInt32(eventsBuffer, events.Length);
            Marshal.WriteIntPtr(eventsBuffer, 4, IntPtr.Zero);
            int headerOffset = ptrBytes + 4;
            int eventOffset = headerOffset + ptrBytes + (ptrBytes * events.Length);
            for (i = 0; i < events.Length; i++)
            {
               e = events[i]; eventOffset = (eventOffset + 7) & (~7);
               Marshal.WriteIntPtr(eventsBuffer, headerOffset, IntPtr.Add(eventsBuffer, eventOffset));
               headerOffset += ptrBytes;
               switch (e.Type)
               {
                  case VstEventType.Midi:
                     Marshal.WriteInt32(eventsBuffer, eventOffset, (int)VstEventType.Midi); eventOffset += 4;
                     Marshal.WriteInt32(eventsBuffer, eventOffset, 32); eventOffset += 4;
                     Marshal.WriteInt32(eventsBuffer, eventOffset, e.SampleIndex); eventOffset += 4;
                     Marshal.WriteInt32(eventsBuffer, eventOffset, e.Flags); eventOffset += 4;
                     Marshal.WriteInt32(eventsBuffer, eventOffset, e.Data_0_3); eventOffset += 4; // noteLength
                     Marshal.WriteInt32(eventsBuffer, eventOffset, e.Data_4_7); eventOffset += 4; // noteOffset
                     Marshal.WriteInt32(eventsBuffer, eventOffset, e.Data_8_11 & 0xFFFFFF); eventOffset += 4; // midi
                     Marshal.WriteInt32(eventsBuffer, eventOffset, e.Data_12_15 & 0xFFFF); eventOffset += 4; // detune + noteOff
                     break;

                  case VstEventType.MidiSysEx:
                     Marshal.WriteInt32(eventsBuffer, eventOffset, (int)VstEventType.Midi); eventOffset += 4;
                     Marshal.WriteInt32(eventsBuffer, eventOffset, 20 + (3 * ptrBytes)); eventOffset += 4;
                     Marshal.WriteInt32(eventsBuffer, eventOffset, e.SampleIndex); eventOffset += 4;
                     Marshal.WriteInt32(eventsBuffer, eventOffset, e.Flags); eventOffset += 4;
                     Marshal.WriteInt32(eventsBuffer, eventOffset, e.SysExData != null ? e.SysExData.Length : 0); eventOffset += 4; // dumpBytes
                     Marshal.WriteIntPtr(eventsBuffer, eventOffset, IntPtr.Zero); eventOffset += ptrBytes;
                     Marshal.WriteIntPtr(eventsBuffer, eventOffset, IntPtr.Add(eventsBuffer, eventOffset + ptrBytes + ptrBytes)); eventOffset += ptrBytes; // sysexDump
                     Marshal.WriteIntPtr(eventsBuffer, eventOffset, IntPtr.Zero); eventOffset += ptrBytes;
                     if (e.SysExData != null && e.SysExData.Length > 0)
                     {
                        Marshal.Copy(e.SysExData, 0, IntPtr.Add(eventsBuffer, eventOffset), e.SysExData.Length);
                     }
                     break;

                  default:
                     Marshal.WriteInt32(eventsBuffer, eventOffset, (int)e.Type); eventOffset += 4;
                     Marshal.WriteInt32(eventsBuffer, eventOffset, e.ByteSize); eventOffset += 4;
                     if (e.ByteSize >= 8)
                     {
                        Marshal.WriteInt32(eventsBuffer, eventOffset, e.SampleIndex); eventOffset += 4;
                        Marshal.WriteInt32(eventsBuffer, eventOffset, e.Flags); eventOffset += 4;
                        for (s = 0, sm = e.ByteSize - 8; s < sm; s++, eventOffset++) { Marshal.WriteByte(eventsBuffer, eventOffset, e[s]); }
                     }
                     else
                     {
                        for (s = 0, sm = e.ByteSize; s < sm; s++, eventOffset++) { Marshal.WriteByte(eventsBuffer, eventOffset, 0); }
                     }
                     break;
               }
            }

            if (!isProcessing) { StartProcess(); }
            DispatcherDelegate(EffectHandle, effProcessEvents, 0, IntPtr.Zero, eventsBuffer, 0);
            eventsProcessed = true;
         }
      }

      public void ProcessReplacing(float[][] inputs, float[][] outputs, int bufferSize = 0)
      {
         lock (this)
         {
            int b, i;
            if (LibraryHandle == IntPtr.Zero || ProcessReplacingDelegate == null) { return; }
            if (bufferSize <= 0 || bufferSize > blockSize) { bufferSize = blockSize; }

            if (audioBuffer == IntPtr.Zero)
            {
               if (blockSize < 1) { blockSize = 1; }
               blockBytes = blockSize * 4;
               audioOutputArrayOffset = InputCount * ptrBytes;
               audioInputOffset = OutputCount * ptrBytes + audioOutputArrayOffset;
               audioOutputOffset = InputCount * blockBytes + audioInputOffset;
               audioBufferSize = OutputCount * blockBytes + audioOutputOffset;
               audioBuffer = Marshal.AllocHGlobal(audioBufferSize);
               RtlZeroMemory(audioBuffer, audioBufferSize);
               for (b = 0, i = 0; b < InputCount; b++, i += ptrBytes)
               {
                  Marshal.WriteIntPtr(audioBuffer, i, IntPtr.Add(audioBuffer, b * blockBytes + audioInputOffset));
               }
               for (b = 0, i = audioOutputArrayOffset; b < OutputCount; b++, i += ptrBytes)
               {
                  Marshal.WriteIntPtr(audioBuffer, i, IntPtr.Add(audioBuffer, b * blockBytes + audioOutputOffset));
               }
            }
            if (inputs == null)
            {
               if (InputCount > 0) { RtlZeroMemory(IntPtr.Add(audioBuffer, audioInputOffset), blockBytes * InputCount); }
            }
            else
            {
               for (b = 0, i = Math.Min(inputs.Length, InputCount); b < i; b++)
               {
                  float[] buffer = inputs[b];
                  int copy = buffer != null ? Math.Min(buffer.Length, blockSize) : 0;
                  if (copy > 0)
                  {
                     Marshal.Copy(buffer, 0, IntPtr.Add(audioBuffer, b * blockBytes + audioInputOffset), copy);
                  }
                  if (copy < blockSize)
                  {
                     RtlZeroMemory(IntPtr.Add(audioBuffer, b * blockBytes + copy * 4 + audioInputOffset), (blockSize - copy) * 4);
                  }
               }
               if (i < InputCount)
               {
                  RtlZeroMemory(IntPtr.Add(audioBuffer, i * blockBytes + audioInputOffset), (InputCount - i) * blockBytes);
               }
            }

            if (!isProcessing) { StartProcess(); }
            if (CanProcessEvents && !eventsProcessed) { ProcessEvents(null); }
            ProcessReplacingDelegate(EffectHandle, audioBuffer, IntPtr.Add(audioBuffer, audioOutputArrayOffset), bufferSize);
            eventsProcessed = false;

            if (outputs != null && outputs.Length > 0)
            {
               for (b = 0, i = Math.Min(outputs.Length, OutputCount); b < i; b++)
               {
                  float[] buffer = outputs[b];
                  if (buffer == null) { continue; }
                  int copy = Math.Min(buffer.Length, blockSize);
                  if (copy > 0)
                  {
                     Marshal.Copy(IntPtr.Add(audioBuffer, b * blockBytes + audioOutputOffset), buffer, 0, copy);
                  }
                  if (copy < buffer.Length)
                  {
                     Array.Clear(buffer, copy, buffer.Length - copy);
                  }
               }
               if (outputs.Length == 1 && OutputCount > 1 && outputs[0] != null)
               {
                  if (mixBuffer == null || mixBuffer.Length < blockSize) { mixBuffer = new float[blockSize]; }
                  Marshal.Copy(IntPtr.Add(audioBuffer, blockBytes + audioOutputOffset), mixBuffer, 0, blockSize);
                  float[] buffer = outputs[0];
                  int copy = Math.Min(buffer.Length, blockSize);
                  for (i = 0; i < copy; i++) { buffer[i] = (buffer[i] + mixBuffer[i]) * 0.5f; }
               }
               else
               {
                  for (; i < outputs.Length; i++)
                  {
                     if (outputs[i] == null) { continue; }
                     if (i == 1 && outputs[0] != null)
                     {
                        Array.Copy(outputs[0], outputs[1], Math.Min(outputs[0].Length, outputs[1].Length));
                     }
                     else
                     {
                        Array.Clear(outputs[i], 0, outputs[i].Length);
                     }
                  }
               }
            }
         }
      }

      public VstLibraryContext Clone()
      {
         VstLibraryContext newInstance = new VstLibraryContext(FileName);
         for (int i = 0; i < Parameters.Length; i++) { newInstance.SetParameter(i, GetParameter(i)); }
         byte[] data = GetChunk(); if (data != null) { newInstance.SetChunk(data); }
         return (newInstance);
      }

      public void Dispose()
      {
         lock (this)
         {
            if (LibraryHandle == IntPtr.Zero) { return; }
            try { Off(); }
            catch { }
            if (isEditorOpen)
            {
               try { EditorClose(); }
               catch { }
            }
            try { Close(); }
            catch { }
            try { Marshal.FreeHGlobal(Buffer); }
            catch { }
            if (audioBuffer != IntPtr.Zero)
            {
               try { Marshal.FreeHGlobal(audioBuffer); }
               catch { }
            }
            if (eventsBuffer != IntPtr.Zero)
            {
               try { Marshal.FreeHGlobal(eventsBuffer); }
               catch { }
            }
            try { FreeLibrary(LibraryHandle); }
            catch { }
            LibraryHandle = IntPtr.Zero;
         }
      }

      #endregion
   }
}

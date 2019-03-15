using System.Runtime.InteropServices;

namespace VST.NET
{
   [StructLayout(LayoutKind.Sequential)]
   public struct VstRectangle
   {
      public short Top;
      public short Left;
      public short Bottom;
      public short Right;

      public int Width { get { return (Right - Left); } }
      public int Height { get { return (Bottom - Top); } }
   }
}

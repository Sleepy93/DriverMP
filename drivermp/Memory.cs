using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace MemoryEdit
{
    class Memory
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(UInt32 dwDesiredAccess, Boolean bInheritHandle,
        UInt32 dwProcessId);
        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
        byte[] lpBuffer, UIntPtr nSize, uint lpNumberOfBytesWritten);
        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
        byte[] lpBuffer, UIntPtr nSize, uint lpNumberOfBytesWritten);

        //Create handle
        IntPtr Handle;

        //constructor
        public Memory(Process sprocess, uint access)
        {
            //Get the specific process
            Process nProcess = Process.GetProcessById(sprocess.Id);
            //access to the process
            //0x10 - read
            //0x20 - write
            //0x001F0FFF - all
            Handle = OpenProcess(access, false, (uint)nProcess.Id);
        }

        //Memory reading

        public byte[] ReadByte(uint pointer, int blen)
        {
            byte[] bytes = new byte[blen];

            //Reading the specific address within the process
            ReadProcessMemory(Handle, (IntPtr)pointer, bytes, (UIntPtr)blen, 0);
            return bytes;
        }

        public byte ReadByte2(uint pointer)
        {
            byte[] bytes = new byte[1];

            //Reading the specific address within the process
            ReadProcessMemory(Handle, (IntPtr)pointer, bytes, (UIntPtr)1, 0);
            return bytes[0];
        }

        //Memory writing

        //Byte
        public void WriteByte(uint pointer, byte[] Buffer, int blen)
        {
            WriteProcessMemory(Handle, (IntPtr)pointer, Buffer, (UIntPtr)blen, 0);
        }
    }
}
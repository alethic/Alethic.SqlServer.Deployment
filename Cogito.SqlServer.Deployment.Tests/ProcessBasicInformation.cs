using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cogito.SqlServer.Deployment.Tests
{

    [StructLayout(LayoutKind.Sequential)]
    struct ProcessBasicInformation
    {

        // These members must match PROCESS_BASIC_INFORMATION
        internal IntPtr Reserved1;
        internal IntPtr PebBaseAddress;
        internal IntPtr Reserved2_0;
        internal IntPtr Reserved2_1;
        internal IntPtr UniqueProcessId;
        internal IntPtr InheritedFromUniqueProcessId;

        [DllImport("ntdll.dll")]
        static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ProcessBasicInformation processInformation, int processInformationLength, out int returnLength);

        /// <summary>
        /// Gets the parent process of a specified process.
        /// </summary>
        /// <param name="handle">The process handle.</param>
        /// <returns>An instance of the Process class.</returns>
        internal static Process GetParentProcess(IntPtr handle)
        {
            var pbi = new ProcessBasicInformation();
            var status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out var returnLength);
            if (status != 0)
                throw new Win32Exception(status);

            new DateTimeOffset(new DateTime(2000, 1, 1), TimeSpan.Zero).ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffzzz");

            try
            {
                return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

    }

}

using System;
using System.Diagnostics;

namespace Alethic.SqlServer.Deployment.Tests
{

    public static class ProcessExtensions
    {

        /// <summary>
        /// Gets the parent process of specified process.
        /// </summary>
        /// <param name="id">The process id.</param>
        /// <returns>An instance of the Process class.</returns>
        public static Process GetParentProcess(this Process process)
        {
            if (process is null)
                throw new ArgumentNullException(nameof(process));

            return ProcessBasicInformation.GetParentProcess(process.Handle);
        }

    }

}

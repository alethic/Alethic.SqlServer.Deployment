using System.Diagnostics;

namespace Cogito.SqlServer.Deployment.Tests
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
            return ProcessBasicInformation.GetParentProcess(process.Handle);
        }

    }

}

namespace Cogito.SqlServer.Deployment.Tests.Framework
{

    /// <summary>
    /// Describes the requested SQL environment.
    /// </summary>
    public class SqlTestEnvironmentInfo
    {

        readonly string uniqueKey;
        readonly bool temporary;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <param name="temporary"></param>
        internal SqlTestEnvironmentInfo(int uniqueId, bool temporary)
        {
            this.uniqueKey = uniqueId.ToString("X8");
            this.temporary = temporary;
        }

        /// <summary>
        /// Gets the unique instance ID of the environment.
        /// </summary>
        public string UniqueId => uniqueKey;

        /// <summary>
        /// Gets whether the environment is temporary.
        /// </summary>
        public bool Temporary => temporary;

    }

}

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes the package deployment lock mode.
    /// </summary>
    public enum SqlPackageLockMode
    {

        /// <summary>
        /// No lock is established during the deployment.
        /// </summary>
        None = 0,
        
        /// <summary>
        /// A server lock is established. This results in an app_lock on the master database for the duration of the deployment.
        /// </summary>
        Server = 1,

        /// <summary>
        /// A database lock is established. This results in an app_lock on the target database for the duration of the deployment. Required for contained database user scenarios (Azure SQL).
        /// </summary>
        Database = 2,

    }

}

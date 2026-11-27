namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// The kind of a server login, as CREATE LOGIN distinguishes them.
    /// </summary>
    public enum SqlDeploymentLoginType
    {

        /// <summary>
        /// A SQL Server login, authenticated by its password (CREATE LOGIN ... WITH PASSWORD).
        /// </summary>
        Sql,

        /// <summary>
        /// A Windows login: a domain or local account, a group, or a (group) managed service account
        /// (CREATE LOGIN ... FROM WINDOWS).
        /// </summary>
        Windows,

    }

}

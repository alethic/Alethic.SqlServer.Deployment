using System;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes a SQL server instance.
    /// </summary>
    public class SqlInstance
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="connectionString"></param>
        public SqlInstance(string name, string connectionString)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Name of the SQL server.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Authentication method for contacting SQL.
        /// </summary>
        public string ConnectionString { get; }

    }

}

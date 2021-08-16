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
        /// <param name="authentication"></param>
        public SqlInstance(string name, SqlAuthenticationMethod authentication)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Authentication = authentication;
        }

        /// <summary>
        /// Name of the SQL server.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Authentication method for contacting SQL.
        /// </summary>
        public SqlAuthenticationMethod Authentication { get; }

        /// <summary>
        /// If using a password based authentication flow.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// If using a password based authentication flow.
        /// </summary>
        public string Password { get;  }

    }

}

using System;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes an exception in processing a SQL Deployment.
    /// </summary>
    public class SqlDeploymentException : Exception
    {

        public SqlDeploymentException()
        {

        }

        public SqlDeploymentException(string message) : base(message)
        {

        }

        public SqlDeploymentException(string message, Exception innerException) : base(message, innerException)
        {

        }

    }

}

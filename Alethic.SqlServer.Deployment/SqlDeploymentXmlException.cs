using System;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes an exception in processing SQL Deployment XML.
    /// </summary>
    public class SqlDeploymentXmlException : Exception
    {

        public SqlDeploymentXmlException()
        {

        }

        public SqlDeploymentXmlException(string message) : base(message)
        {

        }

        public SqlDeploymentXmlException(string message, Exception innerException) : base(message, innerException)
        {

        }

    }

}

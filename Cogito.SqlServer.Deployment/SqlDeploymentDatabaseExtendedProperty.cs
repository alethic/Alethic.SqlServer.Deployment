using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Applies an extended property to a database.
    /// </summary>
    public class SqlDeploymentDatabaseExtendedProperty
    {

        /// <summary>
        /// Gets the name of the extended property to apply.
        /// </summary>
        public SqlDeploymentExpression Name { get; set; }

        /// <summary>
        /// Gets the value of the extended property to apply.
        /// </summary>
        public SqlDeploymentExpression Value { get; set; }

        /// <summary>
        /// Compiles the extended property action.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context, string databaseName)
        {
            yield return new SqlDeploymentDatabaseExtendedPropertyAction(context.InstanceName, databaseName, Name, Value);
        }

    }

}

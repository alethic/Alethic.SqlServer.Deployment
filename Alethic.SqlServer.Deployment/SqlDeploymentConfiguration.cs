using System;
using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes configuration properties to be configured on the database.
    /// </summary>
    public class SqlDeploymentConfiguration : Dictionary<string, SqlDeploymentExpression>
    {

        public new SqlDeploymentExpression? this[string name]
        {
            get => TryGetValue(name, out var v) ? v : null;
            set => base[name] = value ?? throw new ArgumentNullException(nameof(value));
        }

        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context)
        {
            foreach (var kvp in this)
                yield return new SqlDeploymentConfigurationAction(context.InstanceName, kvp.Key, kvp.Value.Expand<int>(context));
        }

    }

}

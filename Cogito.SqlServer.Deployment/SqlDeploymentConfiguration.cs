using System;
using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentConfiguration : Dictionary<string, SqlDeploymentExpression>
    {

        public new SqlDeploymentExpression? this[string name]
        {
            get => TryGetValue(name, out var v) ? v : null;
            set => base[name] = value ?? throw new ArgumentNullException(nameof(value));
        }

        public IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            foreach (var kvp in this)
                yield return new SqlDeploymentConfigurationStep(context.InstanceName, kvp.Key, kvp.Value.Expand<int>(context));
        }

    }

}

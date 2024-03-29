﻿using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Specifies the configuration of the server to refer to a remote distributor.
    /// </summary>
    public class SqlDeploymentPublisher
    {

        /// <summary>
        /// Gets or sets the name of the distributor instance. If not specified, configures the local instance as the distributor.
        /// </summary>
        public SqlDeploymentExpression? DistributorInstanceName { get; set; }

        /// <summary>
        /// Gets or sets the authentication method for the distributor instance.
        /// </summary>
        public SqlDeploymentExpression? DistributorConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the admin password to use for connecting to the existing distributor instance.
        /// </summary>
        public SqlDeploymentExpression? DistributorAdminPassword { get; set; }

        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublisherAction(context.Instance)
            {
                DistributorInstance = new SqlInstance(DistributorInstanceName?.Expand(context), DistributorConnectionString?.Expand(context)),
                DistributorAdminPassword = DistributorAdminPassword?.Expand(context),
            };
        }

    }

}


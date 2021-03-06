﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alethic.SqlServer.Deployment
{

    public class SqlDeploymentPublicationMergeAction : SqlDeploymentPublicationAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="name"></param>
        public SqlDeploymentPublicationMergeAction(string instanceName, string databaseName, string name) :
            base(instanceName, databaseName, name)
        {

        }

        public override Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

    }

}

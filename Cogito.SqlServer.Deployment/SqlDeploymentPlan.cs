﻿using System;
using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a compiled SQL deployment plan.
    /// </summary>
    public class SqlDeploymentPlan
    {

        readonly Dictionary<string, SqlDeploymentPlanTarget> targets;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="targets"></param>
        internal SqlDeploymentPlan(Dictionary<string, SqlDeploymentPlanTarget> targets)
        {
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
        }

        /// <summary>
        /// Gets the plan targets available within the plan.
        /// </summary>
        internal Dictionary<string, SqlDeploymentPlanTarget> Targets => targets;

        /// <summary>
        /// Creates a new executor for the plan.
        /// </summary>
        /// <returns></returns>
        public SqlDeploymentExecutor CreateExecutor() => new SqlDeploymentExecutor(this);

    }

}

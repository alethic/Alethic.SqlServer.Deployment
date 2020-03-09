using System;
using System.Collections.Generic;
using System.Linq;

namespace Cogito.SqlServer.Deployment.Tool
{

    /// <summary>
    /// Maps a known instance name to a full instance name.
    /// </summary>
    public class SqlInstanceMap : Dictionary<string, string>
    {

        readonly IEnumerable<(string, string)> values;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="values"></param>
        public SqlInstanceMap(IEnumerable<(string, string)> values) :
            base(values.ToDictionary(i => i.Item1, i => i.Item2))
        {
            this.values = values ?? throw new ArgumentNullException(nameof(values));
        }

    }

}

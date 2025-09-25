using System;
using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes an interpolated string used as a property within a SQL deployment.
    /// </summary>
    public readonly struct SqlDeploymentExpression
    {

        public static implicit operator string(SqlDeploymentExpression value)
        {
            return value.expression;
        }

        public static implicit operator SqlDeploymentExpression(string value)
        {
            return new SqlDeploymentExpression(value);
        }

        public static implicit operator string(SqlDeploymentExpression? value)
        {
            return value?.expression;
        }

        public static implicit operator SqlDeploymentExpression?(string value)
        {
            if (value == null)
                return null;
            else
                return new SqlDeploymentExpression(value);
        }

        readonly string expression;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="expression"></param>
        public SqlDeploymentExpression(string expression)
        {
            this.expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        /// <summary>
        /// Gets the code that represents the non-expanded version of the variable string.
        /// </summary>
        public string Expression => expression;

        /// <summary>
        /// Expands the value of the string given the specified deployment context.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public string Expand(SqlDeploymentCompileContext context)
        {
            return Expand<string>(context.Arguments);
        }


        /// <summary>
        /// Expands the value of the string given the specified deployment context.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public T Expand<T>(SqlDeploymentCompileContext context)
        {
            return Expand<T>(context.Arguments);
        }

        /// <summary>
        /// Expands the value of the string given the specified argument set.
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public T Expand<T>(IDictionary<string, string> arguments)
        {
            var cont = true;
            var expr = expression;

            while (cont)
            {
                // stop unless some replacement occurs
                cont = false;

                // check for each argument
                foreach (var kvp in arguments)
                {
                    var i = expr.IndexOf('[' + kvp.Key + ']');
                    if (i > -1)
                    {
                        expr = expr.Replace('[' + kvp.Key + ']', kvp.Value);
                        cont = true;
                    }
                }
            }

            // enums can be parsed as string
            if (typeof(T).IsEnum)
                return (T)Enum.Parse(typeof(T), expr);

            // convert expression to target type
            return (T)Convert.ChangeType(expr, typeof(T));
        }

    }

}
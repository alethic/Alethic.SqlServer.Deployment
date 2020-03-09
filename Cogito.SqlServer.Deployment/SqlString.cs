using System;

namespace Cogito.SqlServer.Deployment
{

    public readonly struct SqlString
    {

        public static implicit operator SqlString(string value)
        {
            return new SqlString(value);
        }

        public static implicit operator SqlString(FormattableString formattable)
        {
            return new SqlString(formattable.ToString());
        }

        public string Value { get; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="value"></param>
        private SqlString(string value)
        {
            Value = value;
        }

    }

}

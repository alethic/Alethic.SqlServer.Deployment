using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures a CLR assembly is registered in the instance's trusted assembly list, so it can be
    /// created under 'clr strict security' (SQL Server 2017 and later) without signing the assembly
    /// or enabling TRUSTWORTHY on the database.
    /// </summary>
    public class SqlDeploymentTrustedAssembly
    {

        /// <summary>
        /// Path to the assembly file whose hash to register. Relative paths resolve against the
        /// manifest's directory. Ignored when <see cref="Hash"/> is provided.
        /// </summary>
        public SqlDeploymentExpression? Source { get; set; }

        /// <summary>
        /// The SHA-512 hash to register, as a hexadecimal string (an optional '0x' prefix is
        /// allowed). Overrides <see cref="Source"/> when set, so the assembly file need not be
        /// present.
        /// </summary>
        public SqlDeploymentExpression? Hash { get; set; }

        /// <summary>
        /// Optional description recorded alongside the trusted assembly.
        /// </summary>
        public SqlDeploymentExpression? Description { get; set; }

        /// <summary>
        /// Generates the steps required to ensure the trusted assembly.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentTrustedAssemblyAction(context.Instance, ComputeHash(context), Description?.Expand(context));
        }

        /// <summary>
        /// Resolves the SHA-512 hash to register, either from the supplied <see cref="Hash"/> or by
        /// hashing the <see cref="Source"/> file.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        byte[] ComputeHash(SqlDeploymentCompileContext context)
        {
            var hash = Hash?.Expand(context);
            if (string.IsNullOrEmpty(hash) == false)
                return ParseHex(hash);

            var source = Source?.Expand(context);
            if (string.IsNullOrEmpty(source))
                throw new SqlDeploymentException("A TrustedAssembly requires either a Source file or a Hash.");

            if (Path.IsPathRooted(source) == false)
                source = Path.Combine(context.RelativeRoot, source);
            if (File.Exists(source) == false)
                throw new SqlDeploymentException($"Could not find trusted assembly file '{source}'.");

            using (var sha = SHA512.Create())
                return sha.ComputeHash(File.ReadAllBytes(source));
        }

        /// <summary>
        /// Parses a hexadecimal string, with an optional '0x' prefix, into bytes.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static byte[] ParseHex(string value)
        {
            var s = value.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);

            if (s.Length % 2 != 0)
                throw new SqlDeploymentException("A TrustedAssembly Hash must contain an even number of hexadecimal digits.");

            var b = new byte[s.Length / 2];
            for (var i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);

            return b;
        }

    }

}

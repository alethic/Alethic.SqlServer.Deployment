using System;
using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Extensions.Logging;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Registers an assembly's SHA-512 hash in the instance's trusted assembly list
    /// (sys.trusted_assemblies), so that CREATE ASSEMBLY succeeds under 'clr strict security'
    /// without signing the assembly or enabling TRUSTWORTHY. Idempotent, and a no-op on servers
    /// without strict security (before SQL Server 2017). Requires CONTROL SERVER.
    /// </summary>
    public class SqlDeploymentTrustedAssemblyAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="hash"></param>
        /// <param name="description"></param>
        public SqlDeploymentTrustedAssemblyAction(SqlInstance instance, byte[] hash, string description) :
            base(instance)
        {
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            Description = description;
        }

        /// <summary>
        /// Gets the SHA-512 hash of the assembly to trust.
        /// </summary>
        public byte[] Hash { get; }

        /// <summary>
        /// Gets the description recorded alongside the trusted assembly.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Registers the trusted assembly.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);

            // sys.trusted_assemblies exists only where 'clr strict security' applies (SQL Server
            // 2017 and later); on older servers there is no trust list and nothing to register
            var trustList = await cnn.ExecuteScalarAsync($"SELECT OBJECT_ID('sys.trusted_assemblies')", cancellationToken: cancellationToken);
            if (trustList == null || trustList == DBNull.Value)
                return;

            // already trusted?
            if (await cnn.ExecuteScalarAsync($"SELECT 1 FROM sys.trusted_assemblies WHERE [hash] = {Hash}", cancellationToken: cancellationToken) != null)
                return;

            context.Logger.LogInformation("Registering trusted assembly {Description}.", Description ?? "(unnamed)");
            await cnn.ExecuteNonQueryAsync($"EXEC sys.sp_add_trusted_assembly @hash = {Hash}, @description = {Description ?? ""}", cancellationToken: cancellationToken);
        }

    }

}

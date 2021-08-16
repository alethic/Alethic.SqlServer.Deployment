namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Methods of authenticating against SQL server.
    /// </summary>
    public enum SqlAuthenticationMethod
    {

        Password,

        Windows,

        AzureActiveDirectoryPassword,

        AzureActiveDirectoryIntegrated,

        AzureActiveDirectoryInteractive,

        AzureActiveDirectoryServicePrincipal,

        AzureActiveDirectoryDeviceCodeFlow,

        AzureActiveDirectoryManagedIdentity,

    }

}

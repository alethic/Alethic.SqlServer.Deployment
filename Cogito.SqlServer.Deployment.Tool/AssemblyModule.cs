using Autofac;

using Cogito.Autofac;

namespace Cogito.SqlServer.Deployment.Tool
{

    public class AssemblyModule : ModuleBase
    {

        protected override void Register(ContainerBuilder builder)
        {
            builder.RegisterFromAttributes(typeof(AssemblyModule).Assembly);
        }

    }

}

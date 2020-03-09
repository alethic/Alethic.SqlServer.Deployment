using Autofac;

using FileAndServe.Autofac;

namespace Cogito.SqlServer.Deployment.Tool
{

    public class AssemblyModule :
        Module
    {

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterFromAttributes(typeof(AssemblyModule).Assembly);
        }

    }

}

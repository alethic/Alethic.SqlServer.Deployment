# Alethic.SqlServer.Deployment
The `Alethic.SqlServer.Deployment` package provides a library to enable deployment and configuration, in batch, of SQL Server instances. Though DACPACs provide a convient way to package the schema and objects associated with a single database; in a continuous deployment situation often other higher-level SQL Server configuration would be useful to deploy. For instance, Linked Servers. Or multiple DACPACs. Or complex replication topologies.

## Configuration
`Alethic.SqlServer.Deployment` works by processing a SQL deployment manifest file. This file is an XML file which defines a number of named `Target` elements. Each `Target` element can contain one or more `Instance` elements. And within each `Instance` element configuration can be specified.

`Target` elements can specify an optional `Condition` attribute. The attribute value is expanded against the deployment arguments and must result in a boolean value: usually a variable reference such as `Condition="[DeployExtras]"` paired with a `Parameter` defaulting to `false`. A `Target` whose condition evaluates to false compiles to an empty set of steps: it can still be named for execution and used as a dependency, but executes nothing. (In the future the attribute may grow into a full boolean expression language; today it is plain variable expansion.)

`Target` elements can also define `DependsOn` elements to specify `Target`s that must be run successfully first. This allows complex dependency hierarchies to be built, and deployment of a single required `Target` to commense without encuring the cost of deploying more than is strictly necessary for the task. This facilitates unit testing across multiple SQL instances or SQL databases. Unit tests need only initiate the deployment for the target that they specifically require. Parallelism inherit in a dependency model like this is exploited: targets that can execute concurrently do execute concurrently.

The following example demonstrates the configuration of two LocalDB instances, each one containing a single database deployed from a DACPAC.

```
<Deployment xmlns="https://cogito.cx/schemas/SqlServer.Deployment/manifest/2020">
    <Parameter Name="InstanceNameA" DefaultValue="(localdb)\InstanceA" />
    <Parameter Name="InstanceNameB" DefaultValue="(localdb)\InstanceB" />
    <Parameter Name="PathToDacPacA" />
    <Parameter Name="PathToDacPacB" />
    <Target Name="TargetA">
        <Instance Name="[InstanceNameA]">
            <Database Name="DatabaseA" Owner="sa">
                <Package Source="[PathToDacPacA]" />
            </Database>
        </Instance>
    </Target>
    <Target Name="TargetB">
        <Instance Name="[InstanceNameB]">
            <Database Name="DatabaseB" Owner="sa">
                <Package Source="[PathToDacPacB]" />
            </Database>
        </Instance>
    </Target>
</Deployment>
```

### Trusted Assemblies

Under `clr strict security` (the default since SQL Server 2017) a database cannot `CREATE ASSEMBLY` for an unsigned SQLCLR assembly unless the server trusts it. A `TrustedAssembly` element on an `Instance` registers an assembly's hash (via `sys.sp_add_trusted_assembly`) before that instance's databases deploy, so a DACPAC carrying CLR assemblies deploys to a fresh database without signing them or enabling `TRUSTWORTHY`. It is idempotent, a no-op before SQL Server 2017, and requires `CONTROL SERVER`.

Provide the hash one of two ways. Either point at the assembly file with `Source`, which is hashed with SHA-512 at deploy time (relative paths resolve against the manifest directory):

```
<Instance Name="[InstanceName]">
    <Configuration Name="clr enabled" Value="1" />
    <TrustedAssembly Source="MyClrHelpers.dll" Description="MyClrHelpers" />
</Instance>
```

Or supply the SHA-512 directly with `Hash`, as a hexadecimal string with an optional `0x` prefix, so no file need be present at all:

```
<Instance Name="[InstanceName]">
    <Configuration Name="clr enabled" Value="1" />
    <TrustedAssembly Hash="0x2b3c...512-bit-hex..." Description="MyClrHelpers" />
</Instance>
```

`Description` is optional in both cases. When both `Source` and `Hash` are given, `Hash` wins.

### Logins

A `Login` element on an `Instance` ensures a server login exists, before that instance's databases deploy, so the users a DACPAC maps to it can be created. `Type` is required: `Sql` for a SQL Server login (`CREATE LOGIN ... WITH PASSWORD`), or `Windows` for a Windows account, group or managed service account (`CREATE LOGIN ... FROM WINDOWS`).

```
<Instance Name="[InstanceName]">
    <Login Name="[AppLogin]" Type="[AppLoginType]" Password="[AppLoginPassword]" />
</Instance>
```

What is given decides what the step does; what is missing is left alone:

| Type | Password | Login missing | Login exists |
| --- | --- | --- | --- |
| `Sql` | given | created with the password | its password is set to the one given |
| `Sql` | absent | the deployment fails | validated as a SQL login |
| `Windows` | not allowed | created from Windows | validated as a Windows login or group |

An absent or empty `Name` compiles to nothing, so a manifest can wire the login to parameters that default to empty and leave logins alone wherever they are not set. A login that exists as the other type fails the deployment rather than being replaced, and no login is ever dropped. A password that already matches is not reset. Under a dry run the step reports what it would create or change.

### Users

A `User` element on a `Database` ensures a database user mapped to a server login, and its membership of database roles, after the database's `Package` deploys, so the roles its DACPAC defines exist to be joined. It runs on every deployment: unlike a post-deployment script, a matching package tag does not skip it, and a DACPAC cannot model a user whose login differs between environments.

```
<Database Name="[DatabaseName]">
    <Package Source="[PathToDacPac]" />
    <User Login="[AppLogin]">
        <Role Name="app" />
    </User>
</Database>
```

`Name` is the user's name, the login's when absent. The user is found by the login's SID, whatever its name; failing that, a user of its `Name` mapped to another login, or to none, as a restore from another server leaves it, is mapped to the login; failing that, it is created `FOR LOGIN`. It then joins each `Role` it is not already a member of; a role that does not exist fails the deployment. The login's own user in a database it owns is `dbo`, which joins no role. An absent or empty `Login` compiles to nothing. No user is ever dropped, and no membership removed. Under a dry run the step reports what it would create, map or join.

### Packages: when a database deploys

A `Package` records a tag on its database once deployed: a hash of the DACPAC's content and of the SQLCMD variable values it was deployed with. A deployment whose tag matches the database's is skipped, so a database deploys again when the package changes or when its variable values do. The DACPAC's version only guards against deploying an older package over a newer one: a database whose recorded version is newer than the package's is left alone. `IgnoreDacTag` and `IgnoreDacVersion` turn either check off.

This file, if saved as `Example.xml` can be executed from the .NET Core Global Tool:

```
sqldeploy deploy Example.xml -a PathToDacPacA=A.dacpac -a PathToDacPacB=B.dacpac
```

The single `TargetA` target can be deployed by including it on the command line:

```
sqldeploy deploy Example.xml TargetA -a PathToDacPacA=A.dacpac -a PathToDacPacB=B.dacpac
```

Additionally, from C#, the deployment manifest can be loaded, compiled into a plan, and executed:

```
var d = SqlDeployment.Load("Example.xml");
var p = d.Compile(new Dictionary<string, string>() {
    ["PathToDacPacA"] = "A.dacpac",
    ["PathToDacPacB"] = "B.dacpac",
});

await new SqlDeploymentExecutor(p).ExecuteAsync();
```

The `SqlDeploymentExecutor` can be retained and executed multiple times. Targets which have already run will not run twice.

```
var d = SqlDeployment.Load("Example.xml");
var p = d.Compile(new Dictionary<string, string>() {
    ["PathToDacPacA"] = "A.dacpac",
    ["PathToDacPacB"] = "B.dacpac",
});

var e = new SqlDeploymentExecutor(p);
await e.ExecuteAsync("TargetA");
await e.ExecuteAsync("TargetB");
```

Or it can be retained and executed multiple times concurrently from different parts of the code. This would allow multiple unit tests to concurrently obtain the same `SqlDeploymentExecutor` instance and execute different (or the same) targets concurrently so as not to break parallel unit test execution.

```
var d = SqlDeployment.Load("Example.xml");
var p = d.Compile(new Dictionary<string, string>() {
    ["PathToDacPacA"] = "A.dacpac",
    ["PathToDacPacB"] = "B.dacpac",
});

var e = new SqlDeploymentExecutor(p);
var t1 = Task.Run(() => e.ExecuteAsync("TargetA"));
var t2 = Task.Run(() => e.ExecuteAsync("TargetB"));
await Task.WhenAll(t1, t2);
```

## Tool
A .NET Core Global Tool is available as `Alethic.SqlServer.Deployment.Tool`. This tool supports a `deploy` command, accepting the manifest path as an argument; along with additional `-a` arguments to specify arguments.

```
dotnet sqldeploy Environment.xml -a Foo=Bar
```

## Build
The `Alethic.SqlServer.Deployment.Build` package provides MSBuild extensions to an existing C# project which allow `ProjectReference`s to SSDT projects. This allows importing the `.dacpac` output of one SSDT project as content into another library or executable. This way users can build executables or unit test libraries that contain the corresponding SQL schema along with them; deployable at runtime.

This build framework operates by injecting MSBuild targets into referenced `.sqlproj` files that allow enumeration of the SSDT project outputs. Those outputs are collected and made deterministic, before being included into the content output of the depending project.

Determinism of DACPACs is accomplished by repackaging the DACPAC: removing absolute paths from the `model.xml` file, resetting internal timestamps in the `Origin.xml` file, and rebuilding the DACPAC archive without file timestamps.

To make use of this, install the `Alethic.SqlServer.Deployment.Build` NuGet package into your C# project. Then, add a `<ProjectReference>` to the C# project referencing the SSDT project. Then apply the special `CopySqlProjectOutput` metadata item to that `<ProjectReference>` element. During build of the C# project the `.dacpac` will be copied to the output path and included with the application during publish.

```
<ProjectReference Include="..\Foo\Bar.sqlproj">
    <CopySqlProjectOutput>true</CopySqlProjectOutput>
</ProjectReference>
```

## Unit Testing
In addition to deployment of environmental instances, unit testing against complex SQL server instances can be made more convienent by being able to deploy a complex SQL server topology to LocalDB instances.

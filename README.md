# Cogito.SqlServer.Deployment
The `Cogito.SqlServer.Deployment` package provides a library to enable deployment and configuration, in batch, of SQL Server instances. Though DACPACs provide a convient way to package the schema and objects associated with a single database; in a continuous deployment situation often other higher-level SQL Server configuration would be useful to deploy. For instance, Linked Servers. Or multiple DACPACs. Or complex replication topologies.

## Configuration
`Cogito.SqlServer.Deployment` works by processing a SQL deployment manifest file. This file is an XML file which defines a number of named `Target` elements. Each `Target` element can contain one or more `Instance` elements. And within each `Instance` element configuration can be specified.

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
A .NET Core Global Tool is available as `Cogito.SqlServer.Deployment.Tool`. This tool supports a `deploy` command, accepting the manifest path as an argument; along with additional `-a` arguments to specify arguments.

```
dotnet sqldeploy Environment.xml -a Foo=Bar
```

## Build
The `Cogito.SqlServer.Deployment.Build` package provides MSBuild extensions to an existing C# project which allow `ProjectReference`s to SSDT projects. This allows importing the `.dacpac` output of one SSDT project as content into another library or executable. This way users can build executables or unit test libraries that contain the corresponding SQL schema along with them; deployable at runtime.

This build framework operates by injecting MSBuild targets into referenced `.sqlproj` files that allow enumeration of the SSDT project outputs. Those outputs are collected and made deterministic, before being included into the content output of the depending project.

Determinism of DACPACs is accomplished by repackaging the DACPAC: removing absolute paths from the `model.xml` file, resetting internal timestamps in the `Origin.xml` file, and rebuilding the DACPAC archive without file timestamps.

To make use of this, install the `Cogito.SqlServer.Deployment.Build` NuGet package into your C# project. Then, add a `<ProjectReference>` to the C# project referencing the SSDT project. Then apply the special `CopySqlProjectOutput` metadata item to that `<ProjectReference>` element. During build of the C# project the `.dacpac` will be copied to the output path and included with the application during publish.

```
<ProjectReference Include="..\Foo\Bar.sqlproj">
    <CopySqlProjectOutput>true</CopySqlProjectOutput>
</ProjectReference>
```

## Unit Testing
In addition to deployment of environmental instances, unit testing against complex SQL server instances can be made more convienent by being able to deploy a complex SQL server topology to LocalDB instances.

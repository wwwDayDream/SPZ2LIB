using System.Linq;
using System.Threading.Tasks;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.NuGet.Push;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public sealed class BuildContext : FrostingContext
{
    public class Target
    {
        public string ProjectName { get; set; }
        public string InstallDir { get; set; }
        public string Configuration { get; set; }
        public bool Install { get; set; }
        public bool Nuget { get; set; }
    }

    public Target target = new Target();
    public BuildContext(ICakeContext context)
        : base(context)
    {
        target.ProjectName = context.Argument("Project",
            context.Environment.WorkingDirectory.GetParent().GetDirectoryName());

        target.InstallDir = context
            .ExpandEnvironmentVariables(
                (DirectoryPath)context.Argument("InstallDir", context.Environment.Platform.IsWindows() ? "%AppData%/../LocalLow/tobspr Games/shapez 2/" : ""))
            .Collapse()
            .ToString();
        target.Configuration = context.Argument("Config", "Release");
        target.Install = context.HasArgument("Install");
        target.Nuget = context.HasArgument("Nuget") || context.HasArgument("Push");

        context.Environment.WorkingDirectory = context.Environment.WorkingDirectory.GetParent();
    }
}

[TaskName("Build")]
public sealed class HelloTask : FrostingTask<BuildContext>
{
    private DirectoryPath ProjectDir { get; set; }
    private DirectoryPath BinDir { get; set; }
    private DirectoryPath ConfigurationDir { get; set; }

    public override void Run(BuildContext context)
    {
        ProjectDir = context.Environment.WorkingDirectory.Combine(context.target.ProjectName);
        BinDir = ProjectDir.Combine("bin");
        ConfigurationDir = BinDir.Combine(context.target.Configuration);

        if (context.DirectoryExists(ConfigurationDir))
            context.DeleteDirectory(ConfigurationDir, new DeleteDirectorySettings() { Force = true, Recursive = true });

        context.DotNetPack(ProjectDir.ToString(), new DotNetPackSettings()
        {
            Configuration = context.target.Configuration
        });

        context.Log.Information("Build successful!");

        var zipDir = ConfigurationDir.Combine("temp");
        context.CreateDirectory(zipDir.Combine("patchers"));
        context.CreateDirectory(zipDir.Combine("mods"));
        context.CopyFile(ConfigurationDir.Combine("netstandard2.1/")
            .CombineWithFilePath(context.target.ProjectName + ".dll"), zipDir.Combine("patchers")
            .CombineWithFilePath(context.target.ProjectName + ".dll"));

        var outputZip = ConfigurationDir.CombineWithFilePath("Shapez2AppDataExtract.zip");
        context.Zip(zipDir, outputZip);
        context.DeleteDirectory(zipDir, new DeleteDirectorySettings() {Force = true, Recursive = true});

        if (context.target.Install)
        {
            context.Unzip(outputZip, context.target.InstallDir, true);
            context.Log.Information("Installed to " + context.target.InstallDir + "/..");
        }

        var outputNuget = context.GetFiles(ConfigurationDir + "/*.nupkg").FirstOrDefault();
        context.Log.Information("Located Nuget Package: " + outputNuget);
        
        if (context.target.Nuget)
        {
            context.DotNetNuGetPush(outputNuget, new DotNetNuGetPushSettings()
            {
                Source = "https://api.nuget.org/v3/index.json"
            });
        }
    }
}
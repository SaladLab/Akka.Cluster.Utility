#I @"packages/FAKE/tools"
#r "FakeLib.dll"

open Fake
open Fake.Testing.XUnit2

// Directories
let outputDir = "output"
let testDir = outputDir @@ "test"
let nugetDir = outputDir @@ "nuget"
let nugetWorkDir = nugetDir @@ "work"

// Targets
Target "Clean" (fun _ -> 
    CleanDirs [outputDir]
)

Target "Build" (fun _ ->
    !!"./Akka.Cluster.Utility.sln"
    |> MSBuildRelease "" "Rebuild"
    |> Log "Build-Output: "
)

Target "Test" (fun _ ->  
    let xunitToolPath = findToolInSubPath "xunit.console.exe" "./packages/FAKE/xunit.runner.console*/tools"
    ensureDirectory testDir
    !! ("./core/Akka.Cluster.Utility.Tests/bin/Release/Akka.Cluster.Utility.Tests.dll")
    |> xUnit2 (fun p -> 
        {p with 
            ToolPath = xunitToolPath;
            ShadowCopy = false;
            XmlOutputPath = Some (testDir @@ "Akka.Cluster.Utility.Tests.xml") })
)

Target "Nuget" (fun _ ->  
    CopyFiles (nugetWorkDir @@ "lib/net45") ["./core/Akka.Cluster.Utility/bin/Release/Akka.Cluster.Utility.dll"]

    NuGet (fun p -> 
        {p with
            Project = "Akka.Cluster.Utility"
            OutputPath = nugetDir
            WorkingDir = nugetWorkDir
            Version = "0.1.0-dev20151104" }) 
            "./core/Akka.Cluster.Utility/Akka.Cluster.Utility.nuspec"
)

// Build order
"Clean"
  ==> "Build"
  ==> "Test"

// "Build"
//  ==> "Nuget"
  
// start build
RunTargetOrDefault "Deploy"
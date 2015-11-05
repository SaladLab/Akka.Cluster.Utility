#I @"packages/FAKE/tools"
#r "FakeLib.dll"

open Fake
open Fake.Testing.XUnit2
open Fake.AssemblyInfoFile

// ------------------------------------------------------------------------------ Project

let buildSolutionFile = "./Akka.Cluster.Utility.sln"
let buildConfiguration = "Release"

type Project = { 
    Name: string;
    Folder: string;
    AssemblyVersion: string;
    PackageVersion: string;
    Dependencies: (string * string) list;
}

let decoratePrerelease v =
    let couldParse, parsedInt = System.Int32.TryParse(v)
    if couldParse then "build" + (sprintf "%04d" parsedInt) else v

let decoratePackageVersion v =
    if hasBuildParam "nugetprerelease" then
        v + "-" + decoratePrerelease((getBuildParam "nugetprerelease"))
    else
        v

let projects = ([
    { 
        Name="Akka.Cluster.Utility";
        Folder="./core/Akka.Cluster.Utility";
        AssemblyVersion="0.1.0";
        PackageVersion="";
        Dependencies=[("Akka", "1.0.4"); ("Akka.Cluster", "1.0.4.12-beta")];
    }]
    |> List.map (fun p -> { p with PackageVersion=decoratePackageVersion(p.AssemblyVersion) }))

let project name =
    List.filter (fun p -> p.Name = name) projects |> List.head

// ---------------------------------------------------------------------------- Variables

let binDir = "bin"
let testDir = binDir @@ "test"
let nugetDir = binDir @@ "nuget"
let nugetWorkDir = nugetDir @@ "work"

// ------------------------------------------------------------------------------ Targets

Target "Clean" (fun _ -> 
    CleanDirs [binDir]
    !! ("./core/**/bin/" + buildConfiguration + "/*.Tests.dll")
    |> Seq.iter (fun p -> trace ("!" + p))
)

Target "AssemblyInfo" (fun _ ->
    projects
    |> List.iter (fun p -> 
        CreateCSharpAssemblyInfo (p.Folder @@ "Properties" @@ "AssemblyInfoGenerated.cs")
          [ Attribute.Version p.AssemblyVersion
            Attribute.FileVersion p.AssemblyVersion ]
        )
)

Target "Build" (fun _ ->
    !! buildSolutionFile
    |> MSBuild "" "Rebuild" [ "Configuration", buildConfiguration ]
    |> Log "Build-Output: "
)

Target "Test" (fun _ ->  
    let xunitToolPath = findToolInSubPath "xunit.console.exe" "./packages/FAKE/xunit.runner.console*/tools"
    ensureDirectory testDir
    !! ("./core/**/bin/" + buildConfiguration + "/*.Tests.dll")
    |> xUnit2 (fun p -> 
        {p with 
            ToolPath = xunitToolPath;
            ShadowCopy = false;
            XmlOutputPath = Some (testDir @@ "TestResult.xml") })
)

let createNugetPackages _ =
    projects
    |> List.iter (fun project -> 
        let nugetFile = project.Folder @@ project.Name + ".nuspec";
        let workDir = nugetWorkDir @@ project.Name;

        let dllFileName = project.Folder @@ "bin/Release" @@ project.Name;
        (!! (dllFileName + ".dll")
         ++ (dllFileName + ".pdb")
         ++ (dllFileName + ".xml")) |> CopyFiles (workDir @@ "lib/net45")

        let isAssemblyInfo f = (filename f).Contains("AssemblyInfo")
        let isSrc f = (hasExt ".cs" f) && not (isAssemblyInfo f)
        CopyDir (workDir @@ @"src") project.Folder isSrc

        NuGet (fun p -> 
            {p with
                Project = project.Name
                OutputPath = nugetDir
                WorkingDir = workDir
                Dependencies = project.Dependencies
                SymbolPackage = NugetSymbolPackage.Nuspec
                Version = project.PackageVersion }) 
                nugetFile
    )

let publishNugetPackages _ =
    projects
    |> List.iter (fun project -> 
        NuGetPublish (fun p -> 
            {p with
                Project = project.Name
                OutputPath = nugetDir
                WorkingDir = nugetDir
                AccessKey = getBuildParamOrDefault "nugetkey" ""
                PublishUrl = getBuildParamOrDefault "nugetpublishurl" ""
                Version = project.PackageVersion })

        if hasBuildParam "nugetpublishurl" then (
            // current FAKE doesn't support publishing symbol package with NuGetPublish.
            // To workaround thid limitation, let's tweak Version to cheat nuget read symbol package
            NuGetPublish (fun p -> 
                {p with
                    Project = project.Name
                    OutputPath = nugetDir
                    WorkingDir = nugetDir
                    AccessKey = getBuildParamOrDefault "nugetkey" ""
                    PublishUrl = getBuildParamOrDefault "nugetpublishurl" ""
                    Version = project.PackageVersion + ".symbols" })
        )
    )

Target "Nuget" <| fun _ ->
    createNugetPackages()
    publishNugetPackages()

Target "CreateNuget" <| fun _ ->
    createNugetPackages()

Target "PublishNuget" <| fun _ ->
    publishNugetPackages()

Target "Help" (fun _ ->  
    List.iter printfn [
      "usage:"
      "build [target]"
      ""
      " Targets for building:"
      " * Build        Build"
      " * Test         Build and Test"
      " * Nuget        Create and publish nugets packages"
      " * CreateNuget  Create nuget packages"
      "                [ "
      " * PublishNuget Publish nugets packages"
      "                [nugetkey={API_KEY}] [nugetpublishurl={PUBLISH_URL}]"
      ""]
)

// --------------------------------------------------------------------------- Dependency

// Build order
"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "Test"

"Build"
  ==> "Nuget"

"Build"
  ==> "CreateNuget"
  
RunTargetOrDefault "Help"

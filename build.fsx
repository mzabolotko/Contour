#r "paket:
nuget Fake.IO.FileSystem
nuget Fake.DotNet.AssemblyInfoFile
nuget Fake.DotNet.MSBuild
nuget Fake.DotNet.Testing.NUnit
nuget Fake.DotNet.Paket
nuget Fake.Core.Target
nuget Fake.Core.ReleaseNotes //"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.DotNet
open Fake.DotNet.Testing
open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators


System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let project = "Contour"
let authors = [ "Mikhail Zabolotko" ]
let summary = "A library contains implementation of several EIP patterns to build the service bus."
let description = """
The package contains abstract interfaces of service bus and specific transport implementation for AMQP/RabbitMQ."""
let license = "MIT License"
let tags = "rabbitmq client servicebus"

let release = ReleaseNotes.parse (System.IO.File.ReadLines "RELEASE_NOTES.md")

let buildDir = @"build\"
let nugetDir = @"nuget\"

let projects =
    !! "Sources/**/*.csproj"

let tests =
    !! "Tests/**/*.csproj"

Target.create "CleanUp" (fun _ ->
    Shell.cleanDirs [ buildDir ]
)

Target.create "BuildVersion" (fun _ ->
    let buildVersion = sprintf "%s-build%s" release.NugetVersion BuildServer.appVeyorBuildVersion
    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" buildVersion) |> ignore
)

Target.create "AssemblyInfo" (fun _ ->
    printfn "%A" release
    let info =
        [ AssemblyInfo.Title project
          AssemblyInfo.Company (authors |> String.concat ",")
          AssemblyInfo.Product project
          AssemblyInfo.Description summary
          AssemblyInfo.Version release.AssemblyVersion
          AssemblyInfo.FileVersion release.AssemblyVersion
          AssemblyInfo.InformationalVersion release.NugetVersion
          AssemblyInfo.Copyright license ]
    AssemblyInfoFile.createCSharp <| "./Sources/" @@ project @@ "/Properties/AssemblyInfo.cs" <| info
)

Target.create "Build" (fun _ ->
    projects
    |> MSBuild.runRelease id "" "Rebuild"
    |> Trace.logItems "Build Target Output: "
)

Target.create "RunUnitTests" (fun _ ->
    tests
    |> MSBuild.runDebug id "" "Rebuild"
    |> Trace.logItems "Build Target Output: "

    !! "Tests/**/bin/Debug/*Common.Tests.dll"
    |> NUnit.Sequential.run (fun p ->
           { p with
                DisableShadowCopy = false
                OutputFile = "TestResults.xml"
                TimeOut = System.TimeSpan.FromMinutes 20. })
)

Target.create "RunAllTests" (fun _ ->
    tests
    |> MSBuild.runDebug id "" "Rebuild"
    |> Trace.logItems "Build Target Output: "

    !! "Tests/**/bin/Debug/*.Tests.dll"
    |> NUnit.Sequential.run (fun p ->
           { p with
                DisableShadowCopy = false
                OutputFile = "TestResults.xml"
                TimeOut = System.TimeSpan.FromMinutes 20. })
)

Target.create "BuildPacket" (fun _ ->
    Paket.pack (fun p ->
                    { p with
                        Version = release.NugetVersion })
)

Target.create "Default" ignore

"CleanUp"
    =?> ("BuildVersion", (not BuildServer.isLocalBuild))
    ==> "AssemblyInfo"
    ==> "Build"
    ==> "RunUnitTests"
    =?> ("RunAlltests", BuildServer.isLocalBuild)
    ==> "BuildPacket"
    ==> "Default"

Target.runOrDefault "Default"

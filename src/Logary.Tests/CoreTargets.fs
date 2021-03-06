﻿module Logary.Tests.CoreTargets

open Fuchu
open Swensen.Unquote

open Logary
open Logary.Target.TextWriter
open Logary.Targets

open TestDSL

open Fac

let tests =
  testList "CoreTargets" [
    testCase "initialising TextWriter target" <| fun _ ->
      let target = create (TextWriterConf.Default(System.Console.Out, System.Console.Error)) "sample console"
      let instance = target.initer { serviceName = "tests" }
      instance.name =? "sample console"

    testCase "writing with Console target directly" <| fun _ ->
      let stdout = Fac.textWriter ()
      let target = create (TextWriterConf.Default(stdout, stdout)) "writing console target"
      let instance = target |> initTarget { serviceName = "tests" }

      (because "logging with info level and then finalising the target" <| fun () ->
        "Hello World!" |> Log.infoStr |> logTarget instance
        instance |> finaliseTarget
        stdout.ToString())
      |> should contain "Hello World!"
      |> thatsIt

    testCase "``error levels should be to error text writer``" <| fun _ ->
      let out, err = Fac.textWriter (), Fac.textWriter ()
      let target = create (TextWriterConf.Default(out, err)) "error writing"
      let subject = target |> initTarget { serviceName = "tests" }

      (because "logging 'Error line' and 'Fatal line' to the target" <| fun () ->
        Log.errorStr "Error line" |> logTarget subject
        Log.fatalStr "Fatal line" |> logTarget subject
        subject |> finaliseTarget
        err.ToString())
      |> should contain "Error line"
      |> should contain "Fatal line"
      |> thatsIt
    ]
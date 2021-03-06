namespace Logary.Target

// https://github.com/sbtourist/nimrod
module Nimrod =
  open FSharp.Actor
  open NodaTime

  open Logary
  open Targets
  open TargetUtils
  open Logary.Internals.Tcp
  open Logary.Internals.Date

  /// To configure the Nimrod target, just point at some other target, and the Nimrod
  /// target will take care of formatting according to the correct
  /// Nimrod semantics.
  type NimrodConf = { target : TargetInstance }

  let private actorLoop conf metadata =
    (fun (inbox : IActor<_>) ->
      let rec loop () = async { return! running () }
      and running () = async {
          let! msg, mopts = inbox.Receive()
          match msg with
          | Metric m ->
            let ts = m.timestamp.Ticks / NodaConstants.TicksPerSecond
            let ic = System.Globalization.CultureInfo.InvariantCulture
            let m' = sprintf "[nimrod][%i][%s][%s][%s]" ts (m.ToString()) m.path (m.value.ToString(ic))
            { message       = m'
            ; level         = m.level
            ; data          = Map.empty
            ; path          = m.path
            ; tags          = []
            ; ``exception`` = None
            ; timestamp     = m.timestamp }
            |> logTarget conf.target
            return! loop ()
          | Log l ->
            return! running ()
          | Flush chan ->
            chan.Reply Ack
            return! running ()
          | ShutdownTarget ackChan ->
            return! shutdown () }
      and shutdown () = async { return () }
      loop ())

  let create conf = stdNamedTarget (actorLoop conf)

  let [<CompiledName("Create")>] CreateC(conf, name)  = create conf name

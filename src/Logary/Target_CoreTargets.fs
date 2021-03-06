namespace Logary.Target

/// A module implementing a text writer target. Useful for writing to the console output,
/// or writing to a custom text writer.
module TextWriter =
  open FSharp.Actor
  open System.IO

  open Logary
  open Logary.Targets
  open Logary.Formatting
  open Logary.Internals.InternalLogger

  /// Configuration for a text writer
  type TextWriterConf =
    /// A string formatter to specify how to write the log lines
    { formatter  : StringFormatter
    /// the non-error text writer to output to
    ; output     : TextWriter
    /// the error text writer to output to
    ; error      : TextWriter
    /// whether to flush text writer after each line
    ; flush      : bool
    /// the log level that is considered 'important' enough to write to the error text writer
    ; isErrorAt  : LogLevel }
    static member Default(output, error, ?formatter) =
      { formatter    = defaultArg formatter <| JsonFormatter.Default()
      ; output       = output
      ; error        = error
      ; flush        = false
      ; isErrorAt    = LogLevel.Error }

  let private twLoop
    { formatter = formatter
    ; output    = out
    ; error     = err
    ; flush     = flush
    ; isErrorAt = cutOff }
    metadata =
    (fun (inbox : IActor<_>) ->
      let rec loop () = async {
        let! msg, mopt = inbox.Receive()
        match msg with
        | Log l ->
          let tw = if l.level < cutOff then out else err
          (tw.WriteLine : string -> unit) <| formatter.format l
          do (if flush then tw.Flush() else ())
          return! loop ()
        | Metric m ->
          return! loop ()
        | Flush chan ->
          info "%s: flush start" inbox.Id
          out.Flush()
          err.Flush()
          info "%s: flush Ack" inbox.Id
          chan.Reply Ack
          info "%s: recursing loop" inbox.Id
          return! loop ()
        | ShutdownTarget ackChan ->
          info "%s: shutdown Ack" inbox.Id
          out.Dispose()
          if not (obj.ReferenceEquals(out, err)) then err.Dispose() else ()
          ackChan.Reply Ack
          info "%s: shutting down immediately" inbox.Id
          return () }
      loop ())

  let [<CompiledName("Create")>] create conf = TargetUtils.stdNamedTarget (twLoop conf)

  /// Use from C# to create - uses tuple calling convention
  let [<CompiledName("Create")>] createA (formatter, out, err, flush, isErrorAt, name) =
    create
      { formatter = formatter
      ; output    = out
      ; error     = err
      ; flush     = flush
      ; isErrorAt = isErrorAt }
      name

  /// Use with LogaryFactory.New( s => s.Target< HERE >() )
  type Builder(conf, callParent : FactoryApi.ParentCallback<Builder>) =
    member x.WriteTo(out : #TextWriter, err : #TextWriter) =
      ! (callParent <| Builder({ conf with output = out; error = err }, callParent))

    new(callParent : FactoryApi.ParentCallback<_>) =
      Builder(TextWriterConf.Default(System.Console.Out, System.Console.Error), callParent)

    interface Logary.Targets.FactoryApi.SpecificTargetConf with
      member x.Build name = create conf name

/// The console Target for Logary
module Console =
  open Logary
  open Logary.Formatting
  open Logary.Targets

  /// Colours in hex
  type ConsoleColours =
    { foregroundColor : int
    ; backgroundColor : int }

  /// Console configuration structure
  type ConsoleConf =
    { formatter : StringFormatter
    ; colorMap  : (ConsoleColours -> Logary.LogLevel -> ConsoleColours) option }
    static member Default =
      { formatter = StringFormatter.LevelDatetimePathMessage
      ; colorMap  = None (* fun col line -> 0x000000 black *) }
    static member Create formatter =
      { formatter = formatter
      ; colorMap  = None }

  let [<CompiledName("Create")>] create conf =
    // TODO: coloured console output
    TextWriter.create
      { formatter = conf.formatter
      ; output    = System.Console.Out
      ; error     = System.Console.Error
      ; flush     = false
      ; isErrorAt = Error }

  let [<CompiledName("Create")>] createA(name, conf) = create conf name
  let [<CompiledName("Create")>] createB(name) = create ConsoleConf.Default name

  /// Use with LogaryFactory.New( s => s.Target< HERE >() )
  type Builder(conf, callParent : FactoryApi.ParentCallback<Builder>) =

    /// Specify the formatting style to use when logging to the console
    member x.WithFormatter( sf : StringFormatter ) =
      ! (callParent <| Builder({ conf with formatter = sf }, callParent))

    // TODO
    member x.Colourise() =
      ! (callParent <| Builder(conf, callParent))

    new(callParent : FactoryApi.ParentCallback<_>) =
      Builder(ConsoleConf.Default, callParent)

    interface Logary.Targets.FactoryApi.SpecificTargetConf with
      member x.Build name = create conf name

// boolean IsLogging() method, correct by excluded middle
#nowarn "25"

module Debugger =
  open FSharp.Actor
  open System.Diagnostics

  open Logary
  open Logary.Targets
  open Logary.Formatting

  type DebuggerConf =
    { formatter : StringFormatter }
    static member Default =
      { formatter = StringFormatter.LevelDatetimePathMessageNl }
    static member Create ?formatter =
      { formatter = defaultArg formatter (StringFormatter.LevelDatetimePathMessageNl) }

  let private debuggerLoop conf metadata =
    (fun (inbox : IActor<_>) ->
      let formatter = conf.formatter
      let offLevel = 6
      let rec loop () = async {
        let! msg, opts = inbox.Receive()
        match msg with
        | Log l when Debugger.IsLogging() ->
          Debugger.Log(offLevel, l.path, l |> formatter.format)
          return! loop()
        | Log _ ->
          return! loop ()
        | Metric m when Debugger.IsLogging() ->
          Debugger.Log(offLevel, m.path, sprintf "%A" m)
          return! loop ()
        | Flush chan ->
          chan.Reply Ack
          return! loop()
        | ShutdownTarget chan ->
          chan.Reply Ack
          return () }
      loop ())

  let [<CompiledName("Create")>] create conf = TargetUtils.stdNamedTarget (debuggerLoop conf)

  let [<CompiledName("Create")>] createA(conf, name) = create conf name

  /// Use with LogaryFactory.New( s => s.Target< HERE >() )
  type Builder(conf, callParent : FactoryApi.ParentCallback<Builder>) =

    /// Specify the formatting style to use when logging to the debugger
    member x.WithFormatter( sf : StringFormatter ) =
      ! (callParent <| Builder({ conf with formatter = sf }, callParent))

    new(callParent : FactoryApi.ParentCallback<_>) =
      Builder(DebuggerConf.Default, callParent)

    interface Logary.Targets.FactoryApi.SpecificTargetConf with
      member x.Build name = create conf name

module TelegramIndex.Log

open System
open System.IO
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Diagnostics
open LogModel
open MBrace.FsPickler.Json


let jsonSerializer = FsPickler.CreateJsonSerializer(indent = false, omitHeader = true)

let logFileName = "log.txt"

let readAll () = task {
    if not <| File.Exists(logFileName) then
        return List.empty
    else
        let! content = File.ReadAllTextAsync(logFileName)
        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        |> Seq.map jsonSerializer.UnPickleOfString<LogRecord>
        |> List.ofSeq
}

let insert (logRecords: LogRecord list) = task {
    if logRecords.Length > 0 then
        let rs = logRecords |> List.map jsonSerializer.PickleToString
        do! File.AppendAllLinesAsync(logFileName, rs)
}

let reportException (exc: Exception) =
    let err = exc.ToStringDemystified()
    do ConsoleLog.print <| err

let trackTask (trackableTask: Task.TplUnitTask) = ignore <| task {
    try
        do! trackableTask
        return ()
    with e ->
        do reportException e
}

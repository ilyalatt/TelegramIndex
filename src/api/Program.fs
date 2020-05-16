module TelegramIndex.App

open FSharp.Control.Tasks.V2.ContextInsensitive
open System
open System.Diagnostics

let mainAsync (args: string[]) = task {
    do Environment.CurrentDirectory <- System.IO.Path.Combine(Environment.CurrentDirectory, "data")
    let! cfg = Config.readCfg ()
    do Telega.Internal.TgTrace.IsEnabled <- cfg.Trace
    do ConsoleLog.showTrace <- cfg.Trace
    let! tg =
        if cfg.DisableSync then Task.returnM None
        else ConsoleLog.perfAsync "connect to telegram" (fun () -> Telegram.init cfg.Telegram |> Task.map Some)

    try
        let! dataSyncState = ConsoleLog.perfAsync "restore state" DataSync.init
        let memStorage = dataSyncState |> Var.asVar |> Var.map (fun x -> x.MemStorage)
        let photoDownloadService: PhotoDownloadService.Interface = { Telegram = tg }

        do tg |> Option.iter (fun tg ->
            let dataSyncIface: DataSync.Interface = { Telegram = tg; PhotoDownloadService = photoDownloadService }
            do DataSync.runInBackground cfg.Scrapper dataSyncIface dataSyncState
        )

        let api: Api.Interface = { MemStorage = memStorage }
        do! WebApp.run api args
    finally
        tg |> Option.iter (fun tg -> tg.Client.Dispose())
}

[<EntryPoint>]
let main args =
    try
        do mainAsync(args).Wait()
    with e ->
        do printfn "%s" <| e.ToStringDemystified()
    0

module TelegramIndex.App

open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Diagnostics

let runWithoutSync = not << isNull <| System.Environment.GetEnvironmentVariable("DISABLE_SYNC")

do Telega.Internal.TgTrace.IsEnabled <- true

let mainAsync () = task {
    let! cfg = Config.readCfg ()
    let! tg =
        if runWithoutSync then Task.returnM None
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
        do WebApp.run api |> ignore
        do! Task.TplTask.Delay(-1)
    finally
        tg |> Option.iter (fun tg -> tg.Client.Dispose())
}

[<EntryPoint>]
let main argv =
    try
        do mainAsync().Wait()
    with e ->
        do printfn "%s" <| e.ToStringDemystified()
    0

module TelegramIndex.App

open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Diagnostics

let initDb (cfg: Config.StorageConfig) =
    let client = MongoDB.Driver.MongoClient(cfg.MongoUrl)
    let db = client.GetDatabase(cfg.MongoDbName)
    db

let runWithoutSync = System.Environment.GetEnvironmentVariable("DISABLE_SYNC") <> null

let mainAsync () = task {
    let! cfg = Config.readCfg ()
    let db = ConsoleLog.perf "init db" (fun () -> initDb cfg.Storage)
    let log = Log.init db
    let! tg =
        if runWithoutSync then Task.returnM None
        else ConsoleLog.perfAsync "connect to telegram" (fun () -> Telegram.init cfg.Telegram log |> Task.map Some)

    try
        let photoStorage = PhotoStorage.init db
        let! dataSyncState = ConsoleLog.perfAsync "restore state" (fun () -> DataSync.init log)
        let memStorage = dataSyncState |> Var.asVar |> Var.map (fun x -> x.MemStorage)
        let photoDownloadService: PhotoDownloadService.Interface = { PhotoStorage = photoStorage; Telegram = tg; Log = log }

        do tg |> Option.iter (fun tg ->
            let dataSyncIface: DataSync.Interface = { Telegram = tg; PhotoDownloadService = photoDownloadService; Log = log }
            do DataSync.runInBackground cfg.Scrapper dataSyncIface dataSyncState
        )

        let api: Api.Interface = { MemStorage = memStorage; PhotoStorage = photoStorage; Log = log }
        do WebApp.run api
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

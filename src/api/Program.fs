module TelegramIndex.App

open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Diagnostics

let inline perf<'T> (label: string) (action: unit -> 'T) : 'T =
    let sw = Stopwatch.StartNew()
    let res = action ()
    do ConsoleLog.print <| (sprintf "%s %.2fs" <| label <| sw.Elapsed.TotalSeconds)
    res

let inline perfAsync<'T> (label: string) (action: unit -> 'T Task) : 'T Task = task {
    let sw = Stopwatch.StartNew()
    let! res = action ()
    do ConsoleLog.print <| (sprintf "%s %.2fs" <| label <| sw.Elapsed.TotalSeconds)
    return res
}

let initDb (cfg: Config.StorageConfig) =
    let client = MongoDB.Driver.MongoClient(cfg.MongoUrl)
    let db = client.GetDatabase(cfg.MongoDbName)
    db

let runWithoutSync = true

let mainAsync () = task {
    let! cfg = Config.readCfg ()
    let db = perf "init db" (fun () -> initDb cfg.Storage)
    let log = Log.init db
    let! tg =
        if runWithoutSync then Task.returnM None
        else perfAsync "connect to telegram" (fun () -> Telegram.init cfg.Telegram log |> Task.map Some)

    let photoStorage = PhotoStorage.init db
    let! dataSyncState = perfAsync "restore state" (fun () -> DataSync.init log)
    let memStorage = dataSyncState |> Var.asVar |> Var.map (fun x -> x.MemStorage)
    let photoService: PhotoService.Interface = { PhotoStorage = photoStorage; Telegram = tg; Log = log }

    do tg |> Option.iter (fun tg ->
        let dataSyncIface: DataSync.Interface = { Telegram = tg; Log = log }
        do DataSync.runInBackground cfg.Scrapper dataSyncIface dataSyncState
    )

    let api: Api.Interface = { MemStorage = memStorage; PhotoService = photoService; Log = log }
    do WebApp.run api
}

[<EntryPoint>]
let main argv =
    do mainAsync().Wait()
    0

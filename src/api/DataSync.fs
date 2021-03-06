module TelegramIndex.DataSync

open FSharp.Control.Tasks.V2.ContextInsensitive
open FSharp.Control
open FnArgs

type Interface = {
    Telegram: Telegram.Interface
    PhotoDownloadService: PhotoDownloadService.Interface
}

type State = {
    MemStorage: MemStorage.State
    ScraperState: ScraperModel.ScraperState option
}

let init () = task {
    let! rs = Log.readAll()
    let ms = MemStorage.restoreFromLog rs
    let lastScraperRun =
        rs
        |> Seq.rev
        |> Seq.choose (function | LogModel.ScraperState x -> x | _ -> None)
        |> Seq.tryHead
    let lastMessageId = lastScraperRun |> Option.map (fun x -> x.LastMessageId)
    return Var.create <| {
        MemStorage = ms
        ScraperState = lastMessageId |> Option.map (fun x -> { LastMessageId = x })
    }
}

let private longDelay () =
    DelayHelper.delayBetween 3.0 20.0

let private getScraperSeq (initialScraperState: ScraperModel.ScraperState option) (cfg: Config.ScraperConfig) (iface: Interface) = task {
    let tg = iface.Telegram
    let! channel = Telegram.findChannel cfg.ChannelUsername tg
    match channel with
    | None ->
        do printfn "Can not find the channel with nickname '%s'." <| cfg.ChannelUsername
        return AsyncSeq.empty
    | Some channel ->
        let channelPeer = channel |> Telegram.getChannelPeer
        return
            AsyncSeq.initInfinite ignore
            |> AsyncSeq.scanAsync (fun (_, state) _ -> Async.AwaitTask <| task {
                let! res = Scraper.scrape channelPeer state tg
                let newState = res.State

                if state = newState then do! longDelay ()

                return (res.Messages, newState)
            }) ([], initialScraperState)
            // the code below removes duplicates
            |> AsyncSeq.append (AsyncSeq.singleton ([], None))
            |> AsyncSeq.pairwise
            |> AsyncSeq.filter (fun ((_, prevState), (_, newState)) -> prevState <> newState)
            |> AsyncSeq.map snd
}

let private runImpl (cfg: Config.ScraperConfig) (iface: Interface) (stateVar: State Var.Source) = task {
    let! scraperSeq = getScraperSeq stateVar.value.ScraperState cfg iface
    let scraperStream = scraperSeq |> AsyncSeq.toObservable |> AsyncSeq.ofObservableBuffered
    do!
        scraperStream
        |> AsyncSeq.iterAsync (fun (newMsgs, scraperState) -> Async.AwaitTask <| task {
            let newMessagesLog =
                newMsgs |> Seq.map fst
                |> Seq.map LogModel.LogRecord.Message
                |> List.ofSeq
            let inline shouldUpdate x y = MemStorage.shouldUpdateUser x y stateVar.value.MemStorage
            let newUsersLog =
                newMsgs |> Seq.map snd
                |> Seq.filter ((shouldUpdate id)) |> Seq.map LogModel.LogRecord.User
                |> List.ofSeq

            let newPhotos =
               newMsgs |> Seq.map snd
                |> Seq.filter ((shouldUpdate (fun u -> u.PhotoLocation)))
                |> Seq.choose (fun u -> u.PhotoLocation)
                |> List.ofSeq
            do!
                newPhotos
                |> Seq.map (flip PhotoDownloadService.downloadAndSave iface.PhotoDownloadService)
                |> Seq.map Task.ignore
                |> Task.collectUnit

            let rs = List.concat [ newMessagesLog; newUsersLog; [ LogModel.ScraperState scraperState ] ]
            do! Log.insert rs

            do flip Var.update stateVar <| (fun v ->
                { v with
                    MemStorage = MemStorage.update newMsgs stateVar.value.MemStorage
                    ScraperState = scraperState
                }
            )

            let newMsgsCount = List.length <| newMessagesLog
            do ConsoleLog.print <| (
                sprintf "Scraped %d #whois messages."
                <| newMsgsCount
            )
        })
}


let runInBackground cfg iface stateVar = ignore <| task {
   let mutable flag = false
   while not flag do
       try
           do! runImpl cfg iface stateVar
       with err ->
           let isDisconnect = err :? Telega.TgBrokenConnectionException
           if not isDisconnect then
               do Log.reportException err
           if (err :? Telega.TgPasswordNeededException || err :? Telega.TgNotAuthenticatedException) then
               flag <- true

       do! longDelay ()
}

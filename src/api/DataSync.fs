module TelegramIndex.DataSync

open FSharp.Control.Tasks.V2.ContextInsensitive
open FSharp.Control
open FnArgs

type Interface = {
    Telegram: Telegram.Interface
    PhotoDownloadService: PhotoDownloadService.Interface
    Log: Log.Interface
}

type State = {
    MemStorage: MemStorage.State
    ScrapperState: ScrapperModel.ScrapperState option
}

let init log = task {
    let! rs = Log.readAll log
    let ms = MemStorage.restoreFromLog rs
    let lastScrapperRun =
        rs
        |> Seq.rev
        |> Seq.choose (function | LogModel.ScrapperState x -> x | _ -> None)
        |> Seq.tryHead
    let lastMessageId = lastScrapperRun |> Option.map (fun x -> x.LastMessageId)
    return Var.create <| {
        MemStorage = ms
        ScrapperState = lastMessageId |> Option.map (fun x -> { LastMessageId = x })
    }
}

let private longDelay () =
    DelayHelper.delayBetween 3.0 20.0

let private getScrapperSeq (initialScrapperState: ScrapperModel.ScrapperState option) (cfg: Config.ScrapperConfig) (iface: Interface) = task {
    let log = iface.Log
    let tg = iface.Telegram
    let! channel = Telegram.findChannel cfg.ChannelUsername tg
    let channelPeer = channel |> Option.map Telegram.getChannelPeer |> Option.get

    return
        AsyncSeq.initInfinite ignore
        |> AsyncSeq.scanAsync (fun (_, state) _ -> Async.AwaitTask <| task {
            let! res = Scrapper.scrape channelPeer state tg
            let newState = res.State

            if state = newState then do! longDelay ()

            return (res.Messages, newState)
        }) ([], initialScrapperState)
        // the code below removes duplicates
        |> AsyncSeq.append (AsyncSeq.singleton ([], None))
        |> AsyncSeq.pairwise
        |> AsyncSeq.filter (fun ((_, prevState), (_, newState)) -> prevState <> newState)
        |> AsyncSeq.map snd
}

let private runImpl (cfg: Config.ScrapperConfig) (iface: Interface) (stateVar: State Var.Source) = task {
    let log = iface.Log
    let! scrapperSeq = getScrapperSeq stateVar.value.ScrapperState cfg iface
    let scrapperStream = scrapperSeq |> AsyncSeq.toObservable |> AsyncSeq.ofObservableBuffered
    do!
        scrapperStream
        |> AsyncSeq.iterAsync (fun (newMsgs, scrapperState) -> Async.AwaitTask <| task {
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

            let rs = List.concat [ newMessagesLog; newUsersLog; [ LogModel.ScrapperState scrapperState ] ]
            do! Log.insert rs log

            do flip Var.update stateVar <| (fun v ->
                { v with
                    MemStorage = MemStorage.update newMsgs stateVar.value.MemStorage
                    ScrapperState = scrapperState
                }
            )

            let lastScrapedMsg =
                scrapperState |> Option.map (fun x -> x.LastMessageId)
                |> Option.map string |> Option.defaultValue "-"
            let newMsgsCount = List.length <| newMessagesLog
            let newUsersCount = List.length <| newUsersLog
            do ConsoleLog.print <| (
                sprintf "Last scraped message id: %s, new messages: %d, new users: %d"
                <| lastScrapedMsg <| newMsgsCount <| newUsersCount
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
               do! Log.reportException err iface.Log
           if (err :? Telega.TgPasswordNeededException || err :? Telega.TgNotAuthenticatedException) then
               flag <- true

       do! longDelay ()
}

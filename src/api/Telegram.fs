module TelegramIndex.Telegram

open System
open FSharp.Control.Tasks.V2.ContextInsensitive
open Telega
open Telega.Rpc.Dto
open Telega.Rpc.Dto.Types
open Telega.Rpc.Dto.Types.Storage
open LanguageExt.SomeHelp
open TelegramIndex.Config
open TelegramIndex.Utils

type Semaphore = System.Threading.SemaphoreSlim

type Interface = {
    Client: TelegramClient
    Log: Log.Interface
    LastReqTimestamp: DateTime Var.Source
}

let attemptsCount = 3
let repeat (action: 'X -> 'Y Task.RepeatResult Task.TplTask) (state: 'X) (log: Log.Interface)  =
    let mutable counter = 0
    Task.repeat (fun _ -> task {
        do counter <- counter + 1
        try
            let! res = action state
            return res
        with e ->
            do! Log.reportException e log
            return
                if counter >= attemptsCount then Task.RepeatResult.StopAndThrow e
                else Task.RepeatResult.Repeat
    })

let handleAuth (cfg: TgConfig) (tg: TelegramClient) = task {
    let readLine () = System.Console.ReadLine().Trim()
    do printfn "enter phone number"
    let phoneNumber = readLine ()
    let! hash = tg.Auth.SendCode(cfg.ApiHash.ToSome(), phoneNumber.ToSome())
    do printfn "enter telegram code"
    let code = readLine ()
    try
        let! res = tg.Auth.SignIn(phoneNumber.ToSome(), hash.ToSome(), code.ToSome())
        ()
    with :? TgPasswordNeededException ->
        do printfn "enter cloud password"
        let password = readLine()
        let! tgPwd = tg.Auth.GetPasswordInfo()
        let! res = tg.Auth.CheckPassword(tgPwd.ToSome(), password.ToSome())
        ()
}

let createClient (config: Config.TgConfig) (log: Log.Interface) =
    repeat (fun () -> task {
        let! client = TelegramClient.Connect(config.ApiId)
        while not <| client.Auth.IsAuthorized do
            do! handleAuth config client
        return Task.RepeatResult.Done client
    }) () log

let init (config: Config.TgConfig) (log: Log.Interface) = task {
    let! client = createClient config log
    return {
        Client = client
        Log = log
        LastReqTimestamp = Var.create <| DateTime.MinValue
    }
}


type ClientReqType =
| Common
| File

let clientReq (reqType: ClientReqType) (action: TelegramClient -> 'T Task.TplTask) (iface: Interface) =
    repeat (fun () -> task {
        let tg = iface.Client
        try
            try
                let! res = action tg
                return Task.RepeatResult.Done res
            with e when e :? TgPasswordNeededException || e.Message = "AUTH_KEY_UNREGISTERED" ->
                return Task.RepeatResult.StopAndThrow e
        finally
            do Var.set <| DateTime.Now <| iface.LastReqTimestamp
    }) () iface.Log

let batchLimit = 100 // the API limit
let getChannelHistory channelPeer fromId = clientReq Common (fun client -> task {
    let req =
        Functions.Messages.GetHistory(
            peer = channelPeer,
            offsetId = (fromId |> Option.defaultValue 0 |> (+) batchLimit),
            limit = batchLimit,
            minId = (fromId |> Option.defaultValue -1),

            addOffset = 0,
            offsetDate = 0,
            maxId = 0,
            hash = 0
        )
    do ConsoleLog.trace "get_chat_history_start"
    let! resType = client.Call(req)
    let res =
        resType.Match(
            (fun _ -> None),
            channelTag = (fun x -> Some x)
        )
        |> Option.get
    do ConsoleLog.trace "get_chat_history_end"
    return res
})

type ImageFileMimeType =
| Gif
| Jpeg
| Png

type FileMimeType =
| Image of ImageFileMimeType
| Other

let getFileType (fileType: FileType) : FileMimeType =
    fileType.Match(
        (fun () -> FileMimeType.Other),
        gifTag = (fun _ -> Image ImageFileMimeType.Gif),
        jpegTag = (fun _ -> Image ImageFileMimeType.Jpeg),
        pngTag = (fun _ -> Image ImageFileMimeType.Png)
   )

let getFile (file: InputFileLocation) = clientReq File (fun client -> task {
    do ConsoleLog.trace "get_file_start"
    let ms = new System.IO.MemoryStream()
    let! fileType = client.Upload.DownloadFile((ms :> System.IO.Stream).ToSome(), file.ToSome())
    do ConsoleLog.trace "get_file_end"
    return (getFileType fileType, ms.ToArray())
})

let findChannel channelUsername = clientReq Common (fun client -> task {
    do ConsoleLog.trace "get_user_dialogs_start"
    let! res =
        client.Messages.GetDialogs() |> Task.map (
            (Messages.Dialogs.AsTag >> LExt.toOpt >> Option.get)
            >> (fun d -> d.Chats)
            >> Seq.choose (Chat.AsChannelTag >> LExt.toOpt)
            >> Seq.filter (fun x -> x.Username |> LExt.toOpt = Some channelUsername)
            >> Seq.tryHead
        )
    do ConsoleLog.trace "get_user_dialogs_end"
    return res
})

let getChannelPeer (channel: Chat.ChannelTag) =
    let channelPeer =
        InputPeer.ChannelTag(
            channelId = channel.Id,
            accessHash = (channel.AccessHash |> LExt.toOpt |> Option.get)
        )
    channelPeer

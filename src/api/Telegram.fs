module TelegramIndex.Telegram

open FSharp.Control.Tasks.V2.ContextInsensitive
open Telega
open Telega.Rpc.Dto
open Telega.Rpc.Dto.Types
open Telega.Rpc.Dto.Types.Storage
open LanguageExt.SomeHelp
open TelegramIndex.Config
open TelegramIndex.Utils

type Interface = {
    Client: TelegramClient
}

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

let createClient (config: Config.TgConfig) = task {
    let! client = TelegramClient.Connect(config.ApiId)
    while not <| client.Auth.IsAuthorized do
        do! handleAuth config client
    return client
}

let init (config: Config.TgConfig) = task {
    let! client = createClient config
    return {
        Client = client
    }
}


let batchLimit = 100 // the API limit
let getChannelHistory channelPeer fromId (iface: Interface) = task {
    let client = iface.Client
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
}

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

let getFile (file: InputFileLocation) (iface: Interface) = task {
    let client = iface.Client
    do ConsoleLog.trace "get_file_start"
    let ms = new System.IO.MemoryStream()
    let! fileType = client.Upload.DownloadFile((ms :> System.IO.Stream).ToSome(), file.ToSome())
    do ConsoleLog.trace "get_file_end"
    return (getFileType fileType, ms.ToArray())
}

let findChannel channelUsername (iface: Interface) = task {
    let client = iface.Client
    do ConsoleLog.trace "get_user_dialogs_start"
    let! dialogs =
        client.Call(
            Functions.Messages.GetDialogs(
                offsetDate = 0,
                offsetPeer = (InputPeer.SelfTag() |> InputPeer.SelfTag.op_Implicit),
                limit = 100,
                excludePinned = false,
                offsetId = 0,
                hash = 0,
                folderId = LanguageExt.Option.None
            )
        )
    let res =
        dialogs
        |> (Messages.Dialogs.AsTag >> LExt.toOpt >> Option.get)
        |> (fun d -> d.Chats)
        |> Seq.choose (Chat.AsChannelTag >> LExt.toOpt)
        |> Seq.filter (fun x -> x.Username |> LExt.toOpt = Some channelUsername)
        |> Seq.tryHead
    do ConsoleLog.trace "get_user_dialogs_end"
    return res
}

let getChannelPeer (channel: Chat.ChannelTag) =
    let channelPeer =
        InputPeer.ChannelTag(
            channelId = channel.Id,
            accessHash = (channel.AccessHash |> LExt.toOpt |> Option.get)
        )
    channelPeer

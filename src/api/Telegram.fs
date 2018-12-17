module TelegramIndex.Telegram

open FSharp.Control.Tasks.V2.ContextInsensitive
open TLSharp.Core
open TeleSharp.TL
open TeleSharp.TL.Storage
open Cast

type Interface = {
    Client: TelegramClient
    Lock: System.Threading.SemaphoreSlim // because of issues like https://github.com/sochix/TLSharp/issues/492
    Log: Log.Interface
}

let attemptsCount = 3
let repeat (action: 'X -> 'Y Task.RepeatResult Task.TplTask) (state: 'X) (log: Log.Interface)  =
    let mutable counter = 0
    Task.repeat (fun _ -> task {
        do counter <- counter + 1
        do! DelayHelper.delay 3.0 0.3
        try
            let! res = action state
            return res
        with e ->
            do! Log.reportException e log
            return
                if counter >= attemptsCount then Task.RepeatResult.StopAndThrow e
                else Task.RepeatResult.Repeat
    })

let handleAuth (tg: TelegramClient) = task {
    let readLine () = System.Console.ReadLine().Trim()
    do printfn "enter phone number"
    let phoneNumber = readLine ()
    let! hash = tg.SendCodeRequestAsync(phoneNumber)
    do printfn "enter telegram code"
    let code = readLine ()
    try
        let! res = tg.MakeAuthAsync(phoneNumber, hash, code)
        ()
    with :? CloudPasswordNeededException ->
        do printfn "enter cloud password"
        let password = readLine()
        let! tgPwd = tg.GetPasswordSetting()
        let! res = tg.MakeAuthWithPasswordAsync(tgPwd, password)
        ()
}

let createClient (config: Config.TgConfig) (log: Log.Interface) =
    repeat (fun () -> task {
        let client = new TelegramClient(config.ApiId, config.ApiHash)
        do! client.ConnectAsync(true)
        // while not <| client.IsUserAuthorized() do
        //     do! handleAuth client
        return Task.RepeatResult.Done client
    }) () log

let init (config: Config.TgConfig) (log: Log.Interface) = task {
    let! client = createClient config log
    let lock = new System.Threading.SemaphoreSlim(1, 1)
    return { Client = client; Lock = lock; Log = log }
}

let withLock<'T> (action: unit -> 'T Task.TplTask) (iface: Interface) = task {
    let lock = iface.Lock
    do! lock.WaitAsync()
    try
        return! action()
    finally
        do ignore <| lock.Release()
}


let clientReq (action: TelegramClient -> 'T Task.TplTask) (iface: Interface) =
    withLock (fun () ->
        repeat (fun () -> task {
            do! DelayHelper.delay 1.0 0.3
            let tg = iface.Client
            try
                let! res = action tg
                return Task.RepeatResult.Done res
            with e when e :? CloudPasswordNeededException || e.Message = "AUTH_KEY_UNREGISTERED" ->
                do! handleAuth tg
                return Task.RepeatResult.Repeat
        }) () iface.Log
    ) iface

let batchLimit = 100 // the API limit
let getChannelHistory channelPeer fromId = clientReq (fun client -> task {
    let req = Messages.TLRequestGetHistory()
    do req.Peer <- channelPeer
    do req.Limit <- batchLimit
    do req.MinId <- fromId |> Option.defaultValue -1
    do req.OffsetId <- fromId |> Option.defaultValue 0 |> (+) batchLimit
    do ConsoleLog.trace "get_chat_history_start"
    let! res =
        client.SendRequestAsync<Messages.TLAbsMessages>(req)
        |> Task.map castAs<Messages.TLChannelMessages>
    do ConsoleLog.trace "get_chat_history_end"
    return res
})

type ImageFileMimeType =
| Gif = 0xcae1aadf
| Jpeg = 0x7efe0e
| Png = 0xa4f63c0

type FileMimeType =
| Image of ImageFileMimeType
| Other

let getFileType (fileType: TLAbsFileType) : FileMimeType =
    if fileType :? TLFileGif then Image ImageFileMimeType.Gif
    else if fileType :? TLFileJpeg then Image ImageFileMimeType.Jpeg
    else if fileType :? TLFilePng then Image ImageFileMimeType.Png
    else FileMimeType.Other

let getFile (file: TLAbsInputFileLocation) = clientReq (fun client -> task {
    do ConsoleLog.trace "get_file_start"
    let! file = client.GetFile (file, 128 * 1024)
    do ConsoleLog.trace "get_file_end"
    return (getFileType file.Type, file.Bytes)
})

let findChannel channelUsername = clientReq (fun client -> task {
    do ConsoleLog.trace "get_user_dialogs_start"
    let! res =
        client.GetUserDialogsAsync() |> Task.map (
            castAs<Messages.TLDialogs>
            >> (fun d -> d.Chats)
            >> Seq.choose tryCastAs<TLChannel>
            >> Seq.filter (fun x -> x.Username = channelUsername)
            >> Seq.tryHead
        )
    do ConsoleLog.trace "get_user_dialogs_end"
    return res
})

let getChannelPeer (channel: TLChannel) =
    let channelPeer = TLInputPeerChannel()
    do channelPeer.ChannelId <- channel.Id
    do channelPeer.AccessHash <- channel.AccessHash.Value
    channelPeer

module TelegramIndex.PhotoService

open System
open FSharp.Control.Tasks.V2.ContextInsensitive

type Interface = {
    PhotoStorage: PhotoStorage.Interface
    Telegram: Telegram.Interface option
    Log: Log.Interface
}

let downloadAndSave (photoLoc: ScrapperModel.FileLocation) (iface: Interface) = task {
    match iface.Telegram with
    | None -> return None
    | Some tg ->
        let! (mimeType, body) = photoLoc |> Scrapper.fileLocationToInput |> (fun f -> Telegram.getFile f tg)
        let timestamp = DateTimeOffset.UtcNow
        let insertTask = iface.PhotoStorage |> PhotoStorage.insert photoLoc mimeType timestamp body
        do Log.trackTask insertTask iface.Log
        return Some (mimeType, timestamp, body)
}

let getOrDownloadAndSave (photoLoc: ScrapperModel.FileLocation) (iface: Interface) = task {
    let! dbRes = iface.PhotoStorage |> PhotoStorage.find photoLoc
    match dbRes with
    | Some x -> return Some x
    | None -> return! downloadAndSave photoLoc iface
}

module TelegramIndex.PhotoDownloadService

open System
open FSharp.Control.Tasks.V2.ContextInsensitive

type Interface = {
    Telegram: Telegram.Interface option
}

let downloadAndSave (photoLoc: ScrapperModel.PhotoLocation) (iface: Interface) = task {
    match iface.Telegram with
    | None -> return ()
    | Some tg ->
        let! (mimeType, body) = photoLoc |> Scrapper.fileLocationToInput |> (fun f -> Telegram.getFile f tg)
        match mimeType with
        | Telegram.FileMimeType.Image mimeType -> 
            let insertTask = PhotoStorage.insert photoLoc mimeType body
            do Log.trackTask insertTask
            return ()
        | _ ->
            return ()
}

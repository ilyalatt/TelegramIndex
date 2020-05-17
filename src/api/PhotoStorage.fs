module TelegramIndex.PhotoStorage

open System
open System.IO
open FSharp.Control.Tasks.V2.ContextInsensitive

type Photo = {
  Id: string
  Extension: string
  Body: byte array
}

let getId (loc: ScraperModel.PhotoLocation) =
    let inline bts64 (n: int64) = BitConverter.GetBytes n
    let bytes = bts64 loc.PhotoId
    bytes |> BitConverter.ToString |> (fun s -> s.Replace("-", "").ToLower())

let imageDirectory = DirectoryInfo("images")
if not imageDirectory.Exists then imageDirectory.Create()

let find (loc: ScraperModel.PhotoLocation) = task {
    let id = loc |> getId
    let files = imageDirectory.GetFiles(id + ".*")
    match files |> Seq.tryHead with
    | None -> return None
    | Some file ->
        let! content = File.ReadAllBytesAsync(file.FullName)
        let extension = file.Extension
        return Some <| { Id = id; Extension = extension; Body = content }
}

let insert (loc: ScraperModel.PhotoLocation) (mimeType: Telegram.ImageFileMimeType) (body: byte array) = task {
    let id = loc |> getId
    let extension = "." + mimeType.ToString().ToLower()
    let fileName = id + extension
    let path = Path.Combine(imageDirectory.FullName, fileName)
    do! File.WriteAllBytesAsync(path, body)
    return ()
}

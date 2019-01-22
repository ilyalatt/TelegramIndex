module TelegramIndex.PhotoStorage

open System
open FSharp.Control.Tasks.V2.ContextInsensitive

open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver


[<AllowNullLiteral>]
type Photo() =
    [<BsonId>]
    member val Id: byte[] = null with get, set
    member val Timestamp: DateTimeOffset = DateTimeOffset.MinValue with get, set
    member val MimeType: BsonDocument = null with get, set
    member val Body: byte array = null with get, set

type Interface = {
    PhotoCollection: IMongoCollection<Photo>
}

let init (db: IMongoDatabase) =
    { PhotoCollection = db.GetCollection<Photo>("photos") }


let getId (loc: ScrapperModel.FileLocation) =
    let inline bts32 (n: int) = BitConverter.GetBytes n
    let inline bts64 (n: int64) = BitConverter.GetBytes n
    Array.concat [ bts64 loc.VolumeId; bts32 loc.LocalId; bts64 loc.Secret ]

let find (loc: ScrapperModel.FileLocation) (iface: Interface) = task {
    let id = loc |> getId
    let! photoCursor = iface.PhotoCollection.FindAsync(fun x -> x.Id = id)
    let! photo = photoCursor.SingleOrDefaultAsync()
    return if photo = null then None else Some (MongoPickler.unpickle photo.MimeType, photo.Timestamp, photo.Body)
}

let insert (loc: ScrapperModel.FileLocation) (mimeType: Telegram.FileMimeType) (timestamp: DateTimeOffset) (body: byte array) (iface: Interface) = task {
    let id = loc |> getId
    let photo = new Photo(Id = id, Timestamp = timestamp, MimeType = MongoPickler.pickle mimeType, Body = body)
    let! _ = iface.PhotoCollection.ReplaceOneAsync((fun x -> x.Id = id), photo, UpdateOptions(IsUpsert = true))
    return ()
}

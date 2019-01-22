module TelegramIndex.MongoPickler

open System.IO
open MongoDB.Bson
open MongoDB.Bson.Serialization

let private pickler = MBrace.FsPickler.Json.BsonSerializer()

let pickle<'T> (obj: 'T) : BsonDocument =
    let ms = new MemoryStream()
    do pickler.Serialize(ms, obj, leaveOpen = true)
    do ignore <| ms.Seek(0L, SeekOrigin.Begin)
    BsonSerializer.Deserialize(ms)

let unpickle<'T> (bson: BsonDocument) : 'T =
    let ms = new MemoryStream()
    let writer = new IO.BsonBinaryWriter(ms)
    do BsonSerializer.Serialize(writer, bson)
    do ignore <| ms.Seek(0L, SeekOrigin.Begin)
    pickler.Deserialize<'T>(ms)

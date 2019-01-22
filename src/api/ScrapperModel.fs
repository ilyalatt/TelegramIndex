module TelegramIndex.ScrapperModel

open System
open Telega.Rpc.Dto

type ScrapeHistoryResult = {
    Messages: Types.Message list
    Users: Map<int, Types.User>
}

type Message = {
    Id: int
    UserId: int
    Date: DateTimeOffset
    Text: string
}

type FileLocation = {
    VolumeId: int64
    LocalId: int
    Secret: int64
    FileReference: byte[]
}

type User = {
    Id: int
    FirstName: string
    LastName: string
    Username: string option
    PhotoLocation: FileLocation option
}

type ScrapperState = {
    LastMessageId: int
}

type ScrapeResult = {
    Messages: (Message * User) list
    State: ScrapperState option
}

module TelegramIndex.ScraperModel

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

type PeerUser = {
    Id: int
    AccessHash: int64
}

type PhotoLocation = {
    VolumeId: int64
    LocalId: int
    PhotoId: int64
    User: PeerUser
}

type User = {
    Id: int
    FirstName: string
    LastName: string
    Username: string option
    PhotoLocation: PhotoLocation option
}

type ScraperState = {
    LastMessageId: int
}

type ScrapeResult = {
    Messages: (Message * User) list
    State: ScraperState option
}

module TelegramIndex.ScrapperModel

open System
open TeleSharp.TL

type ScrapeHistoryResult = {
    Messages: TLMessage list
    Users: Map<int, TLUser>
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

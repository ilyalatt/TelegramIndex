module TelegramIndex.Config

open MBrace.FsPickler.Json

type TgConfig = {
    ApiId: int
    ApiHash: string
}

type ScrapperConfig = {
    ChannelUsername: string
}

type StorageConfig = {
    MongoUrl: string
    MongoDbName: string
}

type RootConfig = {
    Telegram: TgConfig
    Scrapper: ScrapperConfig
    Storage: StorageConfig
}

let jsonSerializer = FsPickler.CreateJsonSerializer(indent = false, omitHeader = true)
let readCfg () =
    System.IO.File.ReadAllTextAsync("config.json") |> Task.map jsonSerializer.UnPickleOfString<RootConfig>

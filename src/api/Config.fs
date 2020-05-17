module TelegramIndex.Config

open MBrace.FsPickler.Json

type TgConfig = {
    ApiId: int
    ApiHash: string
}

type ScraperConfig = {
    ChannelUsername: string
}

type RootConfig = {
    Telegram: TgConfig
    Scraper: ScraperConfig
    Trace: bool
    DisableSync: bool
}

let jsonSerializer = FsPickler.CreateJsonSerializer(indent = false, omitHeader = true)
let readCfg () =
    System.IO.File.ReadAllTextAsync("config.json")
    |> Task.map jsonSerializer.UnPickleOfString<RootConfig>

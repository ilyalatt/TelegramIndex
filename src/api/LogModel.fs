module TelegramIndex.LogModel

type LogRecord =
| Info of string
| Exception of string
| Message of ScrapperModel.Message
| User of ScrapperModel.User
| ScrapperState of ScrapperModel.ScrapperState option
| FileDownloaded of ScrapperModel.FileLocation

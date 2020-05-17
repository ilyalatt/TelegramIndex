module TelegramIndex.LogModel

type LogRecord =
| Info of string
| Exception of string
| Message of ScraperModel.Message
| User of ScraperModel.User
| ScraperState of ScraperModel.ScraperState option

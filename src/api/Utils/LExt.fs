module TelegramIndex.Utils.LExt

let toOpt<'T> (x: LanguageExt.Option<'T>) = LanguageExt.FSharp.ToFSharp(x)

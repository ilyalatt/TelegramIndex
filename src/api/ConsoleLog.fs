module TelegramIndex.ConsoleLog

let print msg =
    do printfn "%s:  %s" <| System.DateTime.Now.ToString("O") <| msg

let showTrace = true
let trace msg =
    if showTrace then msg |> (+) "TRACE " |> print

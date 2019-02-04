module TelegramIndex.ConsoleLog

open System.Diagnostics
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Threading.Tasks

let print msg =
    do printfn "%s:  %s" <| System.DateTime.Now.ToString("O") <| msg

let showTrace = true
let trace msg =
    if showTrace then msg |> (+) "TRACE " |> print

let perf<'T> (label: string) (action: unit -> 'T) : 'T =
    let sw = Stopwatch.StartNew()
    let res = action ()
    do print <| (sprintf "%s %.2fs" <| label <| sw.Elapsed.TotalSeconds)
    res

let perfAsync<'T> (label: string) (action: unit -> 'T Task) : 'T Task = task {
    let sw = Stopwatch.StartNew()
    let! res = action ()
    do print <| (sprintf "%s %.2fs" <| label <| sw.Elapsed.TotalSeconds)
    return res
}

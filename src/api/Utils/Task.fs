module TelegramIndex.Task

open FSharp.Control.Tasks.V2.ContextInsensitive

type TplUnitTask = System.Threading.Tasks.Task
type TplTask<'T> = System.Threading.Tasks.Task<'T>

let delay (ms: int) =
    TplUnitTask.Delay(ms)

let map<'X, 'Y> (mapper: 'X -> 'Y) (tplTask: 'X TplTask): 'Y TplTask = task {
    let! res = tplTask
    return res |> mapper
}

let returnM<'T> (value: 'T): 'T TplTask = task {
    return value
}

let collect<'T> (seq: 'T TplTask seq): 'T list TplTask = task {
    let mutable res = []
    for tplTask in seq do
        let! taskRes = tplTask
        do res <- taskRes::res
    return List.rev <| res
}

let collectUnit (seq: TplUnitTask seq): TplTask<unit> = task {
    for tplTask in seq do
        do! tplTask
}

let ignore<'T> (tplTask: 'T TplTask) : TplUnitTask =
    tplTask :> TplUnitTask

let cycle<'T> (generator: 'T -> (bool * 'T) TplTask) (state: 'T): 'T TplTask = task {
    let mutable state = state
    let mutable loop = true
    while loop do
        let! (shouldContinue, newState) = generator state
        do state <- newState
        do loop <- shouldContinue
    return state
}

type RepeatResult<'T> =
| StopAndThrow of System.Exception
| Repeat
| Done of 'T

let repeat<'T> (generator: unit -> 'T RepeatResult TplTask): 'T TplTask =
    cycle (fun _ -> task {
        match! generator () with
        | Repeat -> return (true, Unchecked.defaultof<'T>)
        | StopAndThrow e -> return raise e
        | Done res -> return (false, res)
    }) <| Unchecked.defaultof<'T>
